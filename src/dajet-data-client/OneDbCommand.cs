using DaJet.Metadata;
using DaJet.Scripting;
using System.Data;
using System.Data.Common;

namespace DaJet.Data.Client
{
    public sealed class OneDbCommand : DbCommand
    {
        private IDataReader _reader; //TODO: command.NextResult() !!!
        private readonly DbCommand _command;
        private readonly OneDbParameterCollection _parameters;
        private readonly IMetadataProvider _context;
        private string _commandText;
        public OneDbCommand(IMetadataProvider context, DbConnection connection)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            
            _command = connection.CreateCommand();
            _parameters = new OneDbParameterCollection(_command);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader?.Close();
                _reader?.Dispose();
                _command.Dispose();
            }
        }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.Both;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override int CommandTimeout { get; set; } = 30;
        public override string CommandText { get { return _commandText; } set { _commandText = value; } }
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
        public new OneDbParameterCollection Parameters { get { return DbParameterCollection; } }
        protected override DbParameter CreateDbParameter() { throw new NotImplementedException(); }
        protected override OneDbParameterCollection DbParameterCollection { get { return _parameters; } }
        public override void Cancel() { _command.Cancel(); }
        public override void Prepare() { _command.Prepare(); }
        public override int ExecuteNonQuery() { throw new NotImplementedException(); }
        private ScriptDetails ConfigureCommand()
        {
            if (!ScriptProcessor.TryTranspile(in _context, in _commandText, out ScriptDetails result, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _command.CommandText = result.SqlScript;

            _parameters.Clear();

            foreach (var parameter in result.Parameters)
            {
                _parameters.Add(parameter.Key, parameter.Value);
            }

            return result;
        }
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

            DbDataReader reader = _command.ExecuteReader(behavior);

            return new OneDbDataReader(in reader, details.Mappers);
        }
        public IEnumerable<DataObject> Stream()
        {
            ScriptDetails details = ConfigureCommand();

            EntityMapper mapper = details.Mappers[0];

            DataObject record = new(); // memory buffer

            using (IDataReader reader = _command.ExecuteReader())
            {
                while (reader.Read())
                {
                    mapper.Map(in reader, in record);

                    yield return record;
                }
                reader.Close();
            }
        }
        public IEnumerable<T> Stream<T>() where T : class, new()
        {
            ScriptDetails details = ConfigureCommand();

            EntityMapper mapper = details.Mappers[0];

            T record = new(); // memory buffer

            using (IDataReader reader = _command.ExecuteReader())
            {
                while (reader.Read())
                {
                    mapper.Map(in reader, in record);

                    yield return record;
                }
                reader.Close();
            }
        }
    }
}