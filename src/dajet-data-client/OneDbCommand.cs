using DaJet.Metadata;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using Npgsql;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace DaJet.Data.Client
{
    public sealed class OneDbCommand : DbCommand
    {
        private readonly DbCommand _command;
        private readonly IMetadataProvider _context;
        private string _script;
        private readonly Dictionary<string, object> _parameters = new();
        private readonly Func<string, object, DbParameter> AddWithValueDelegate;
        internal OneDbCommand(IMetadataProvider context, DbConnection connection)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            
            _command = connection.CreateCommand();
            _command.Connection = connection;
            _command.CommandType = CommandType.Text;

            if (_command is SqlCommand ms)
            {
                AddWithValueDelegate = ms.Parameters.AddWithValue;
            }
            else if (_command is NpgsqlCommand pg)
            {
                AddWithValueDelegate = pg.Parameters.AddWithValue;
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) { _command.Dispose(); } }

        #region "DbCommand IMPLEMENTATION"
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.Both;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override int CommandTimeout { get; set; } = 30;
        public override string CommandText { get { return _script; } set { _script = value; } }
        protected override DbConnection DbConnection
        {
            get { return _command.Connection; }
            set { _command.Connection = value; }
        }
        protected override DbTransaction DbTransaction
        {
            get { return _command.Transaction; }
            set { _command.Transaction = value; }
        }
        public new Dictionary<string, object> Parameters { get { return _parameters; } }
        protected override DbParameterCollection DbParameterCollection { get { throw new NotImplementedException(); } }
        protected override DbParameter CreateDbParameter() { throw new NotImplementedException(); }
        public override void Cancel() { _command.Cancel(); }
        public override void Prepare() { throw new NotImplementedException(); }
        #endregion

        public override int ExecuteNonQuery()
        {
            _ = ConfigureCommand();

            return _command.ExecuteNonQuery();
        }
        public override object ExecuteScalar()
        {
            using (OneDbDataReader reader = ExecuteReader())
            {
                if (reader.Read())
                {
                    return reader.GetValue(0);
                }
                reader.Close();
            }
            return null;
        }
        public new OneDbDataReader ExecuteReader()
        {
            return ExecuteDbDataReader(CommandBehavior.Default);
        }
        protected override OneDbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            TranspilerResult result = ConfigureCommand();

            DbDataReader reader = _command.ExecuteReader(behavior);

            return new OneDbDataReader(in reader, result.Mappers);
        }
        public IEnumerable<DataObject> StreamReader()
        {
            using (OneDbDataReader reader = ExecuteReader())
            {
                do
                {
                    DataObject record = new(reader.FieldCount); // memory buffer

                    while (reader.Read())
                    {
                        reader.Map(in record);

                        yield return record;
                    }
                }
                while (reader.NextResult());

                reader.Close();
            }
        }
        private TranspilerResult ConfigureCommand()
        {
            // pass user-provided parameters to the transpiler
            // to override corresponding script-defined values

            //TODO: !!! (OneDbCommand) cache transpilation results of the command

            if (!ScriptProcessor.TryProcess(in _context, in _script, in _parameters, out TranspilerResult result, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _command.CommandText = result.SqlScript;

            _command.Parameters.Clear();

            foreach (var parameter in result.Parameters)
            {
                if (parameter.Value is TableValuedParameter tvp)
                {
                    //TODO: move the tvp code to ScriptProcessor !?

                    if (_context.DatabaseProvider == DatabaseProvider.PostgreSql)
                    {
                        object records = CreatePgTableParameter(in tvp);
                        _ = AddWithValueDelegate(tvp.Name, records);
                    }
                    else if (_context.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        List<SqlDataRecord> records = CreateMsTableParameter(in tvp);
                        DbParameter db_parameter = AddWithValueDelegate(tvp.Name, records);
                        if (db_parameter is SqlParameter sql_parameter)
                        {
                            sql_parameter.SqlDbType = SqlDbType.Structured;
                            sql_parameter.TypeName = tvp.DbName;
                        }
                    }
                }
                else
                {
                    _ = AddWithValueDelegate(parameter.Key, parameter.Value);
                }
            }

            return result;
        }
        
        private string GetPgDatabaseName()
        {
            string databaseName = new NpgsqlConnectionStringBuilder(_context.ConnectionString).Database;

            return (string.IsNullOrWhiteSpace(databaseName) ? string.Empty : databaseName);
        }
        private object CreatePgTableParameter(in TableValuedParameter tvp)
        {
            Type type = PgDbConfigurator.GetUserDefinedType(GetPgDatabaseName(), tvp.DbName);

            if (type is null)
            {
                throw new InvalidOperationException($"Type [{tvp.DbName}] is not found.");
            }

            Type list = typeof(List<>).MakeGenericType(new Type[] { type });

            object records = Activator.CreateInstance(list);

            if (records is not IList result)
            {
                throw new InvalidOperationException($"Failed to create instance of [{list}]");
            }

            foreach (var record in tvp.Value)
            {
                object row = Activator.CreateInstance(type);

                int index = 0;

                foreach (var column in record)
                {
                    PropertyInfo property = type.GetProperty(column.Key);

                    if (property is null)
                    {
                        throw new InvalidOperationException($"Property [{column.Key}] is not found.");
                    }

                    if (column.Value is null) { throw new InvalidOperationException("NULL values is not allowed"); }
                    else if (column.Value is bool boolean) { property.SetValue(row, boolean); }
                    else if (column.Value is int integer) { property.SetValue(row, integer); }
                    else if (column.Value is decimal numeric) { property.SetValue(row, numeric); }
                    else if (column.Value is DateTime datetime) { property.SetValue(row, datetime); }
                    else if (column.Value is string text) { property.SetValue(row, text); }
                    else if (column.Value is byte[] binary) { property.SetValue(row, binary); }
                    else if (column.Value is Guid uuid) { property.SetValue(row, uuid.ToByteArray()); }
                    else if (column.Value is Entity entity) { property.SetValue(row, entity.Identity.ToByteArray()); }
                    else { throw new InvalidOperationException("Unsupported data type"); }

                    index++;
                }

                result.Add(row);
            }

            return result;
        }

        private SqlMetaData[] CreateMetaData(in Dictionary<string, object> record)
        {
            SqlMetaData[] metadata = new SqlMetaData[record.Count];

            int index = 0;

            foreach (var item in record) //TODO: build metadata from EntityDefinition !?
            {
                if (item.Value is null) { throw new InvalidOperationException("NULL values is not allowed"); }
                else if (item.Value is bool) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Binary, 1); }
                else if (item.Value is int) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Int); }
                else if (item.Value is decimal) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Decimal, 14, 4); }
                else if (item.Value is DateTime) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.DateTime2); }
                else if (item.Value is string) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.NVarChar, -1); }
                else if (item.Value is byte[]) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.VarBinary, -1); }
                else if (item.Value is Guid) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Binary, 16); }
                else if (item.Value is Entity) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Binary, 16); }
                else { throw new InvalidOperationException("Unsupported data type"); }

                index++;
            }

            return metadata;
        }
        private List<SqlDataRecord> CreateMsTableParameter(in TableValuedParameter table)
        {
            List<SqlDataRecord> records = new();

            if (table.Value is null || table.Value.Count == 0)
            {
                return records;
            }

            SqlMetaData[] metadata = CreateMetaData(table.Value[0]);

            foreach (var record in table.Value)
            {
                SqlDataRecord row = new(metadata);

                int index = 0;

                foreach (var column in record)
                {
                    if (column.Value is null) { throw new InvalidOperationException("NULL values is not allowed"); }
                    else if (column.Value is bool boolean) { row.SetValue(index, boolean ? new byte[] { 1 } : new byte[] { 0 }); }
                    else if (column.Value is int integer) { row.SetInt32(index, integer); }
                    else if (column.Value is decimal numeric) { row.SetDecimal(index, numeric); }
                    else if (column.Value is DateTime datetime) { row.SetDateTime(index, datetime); }
                    else if (column.Value is string text) { row.SetString(index, text); }
                    else if (column.Value is byte[] binary) { row.SetValue(index, binary); }
                    else if (column.Value is Guid uuid) { row.SetValue(index, uuid.ToByteArray()); }
                    else if (column.Value is Entity entity) { row.SetValue(index, entity.Identity.ToByteArray()); }
                    else { throw new InvalidOperationException("Unsupported data type"); }

                    index++;
                }

                records.Add(row);
            }

            return records;
        }

        public IEnumerable<IDataReader> ExecuteNoMagic()
        {
            _command.Parameters.Clear();

            foreach (var parameter in Parameters)
            {
                _ = AddWithValueDelegate(parameter.Key, parameter.Value);
            }

            _command.CommandText = _script;
            _command.CommandType = CommandType.Text;

            using (IDataReader reader = _command.ExecuteReader())
            {
                while (reader.Read())
                {
                    yield return reader;
                }
                reader.Close();
            }
        }
        public int ExecuteNonMagic()
        {
            _command.Parameters.Clear();

            foreach (var parameter in Parameters)
            {
                _ = AddWithValueDelegate(parameter.Key, parameter.Value);
            }

            _command.CommandText = _script;
            _command.CommandType = CommandType.Text;

            return _command.ExecuteNonQuery();
        }
    }
}