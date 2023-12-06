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
        private DbDataReader _reader;
        private readonly DbCommand _command;
        private readonly IMetadataProvider _context;
        private string _script;
        private readonly Dictionary<string, object> _parameters = new();
        public OneDbCommand(IMetadataProvider context, DbConnection connection)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            
            _command = connection.CreateCommand();
            _command.CommandType = CommandType.Text;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _command.Dispose();
            }
        }
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
        public override int ExecuteNonQuery() { throw new NotImplementedException(); }
        public override object ExecuteScalar()
        {
            ScriptDetails details = ConfigureCommand();

            using (DbDataReader reader = _command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return details.Mappers[0].Properties[0].GetValue(reader);
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

            _reader = _command.ExecuteReader(behavior);

            return new OneDbDataReader(_reader, details.Mappers);
        }
        public IEnumerable<T> StreamReader<T>() where T : class, new()
        {
            ScriptDetails details = ConfigureCommand();

            EntityMapper mapper = details.Mappers[0];

            using (IDataReader reader = _command.ExecuteReader())
            {
                T record = new(); // memory buffer

                while (reader.Read())
                {
                    mapper.Map(in reader, in record);

                    yield return record;
                }

                reader.Close();
            }
        }
        public IEnumerable<DataObject> StreamReader()
        {
            ScriptDetails details = ConfigureCommand();

            EntityMapper mapper = details.Mappers[0];

            using (IDataReader reader = _command.ExecuteReader())
            {
                DataObject record = new(mapper.Properties.Count); // memory buffer

                while (reader.Read())
                {
                    mapper.Map(in reader, in record);

                    yield return record;
                }

                reader.Close();
            }
        }
        private ScriptDetails ConfigureCommand()
        {
            // pass user-provided parameters to the transpiler
            // to override corresponding script-defined values

            if (!ScriptProcessor.TryTranspile(in _context, in _script, in _parameters, out ScriptDetails result, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _command.CommandText = result.SqlScript;

            _command.Parameters.Clear();

            if (_command is SqlCommand ms)
            {
                foreach (var parameter in result.Parameters)
                {
                    ms.Parameters.AddWithValue(parameter.Key, parameter.Value);
                }
            }
            else if (_command is NpgsqlCommand pg)
            {
                foreach (var parameter in result.Parameters)
                {
                    pg.Parameters.AddWithValue(parameter.Key, parameter.Value);
                }
            }

            return result;
        }
    }
}