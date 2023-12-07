using DaJet.Metadata;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;
using System.Data.Common;

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
            ScriptDetails _ = ConfigureCommand();

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
            ScriptDetails details = ConfigureCommand();

            DbDataReader reader = _command.ExecuteReader(behavior);

            return new OneDbDataReader(in reader, details.Mappers);
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
        private ScriptDetails ConfigureCommand()
        {
            // pass user-provided parameters to the transpiler
            // to override corresponding script-defined values

            //TODO: !!! (OneDbCommand) cache transpilation results of the command

            if (!ScriptProcessor.TryTranspile(in _context, in _script, in _parameters, out ScriptDetails result, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _command.CommandText = result.SqlScript;

            _command.Parameters.Clear();

            foreach (var parameter in result.Parameters)
            {
                _ = AddWithValueDelegate(parameter.Key, parameter.Value);
            }

            return result;
        }
    }
}