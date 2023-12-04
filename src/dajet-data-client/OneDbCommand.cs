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
        private DbCommand _command;
        private string _commandText;
        private IMetadataProvider _context;
        public OneDbCommand(IMetadataProvider context, DbConnection connection)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            
            _command = connection.CreateCommand();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _command?.Dispose();
            }
            _command = null;
            _context = null;
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
        protected override DbParameter CreateDbParameter() { return _command.CreateParameter(); }
        protected override DbParameterCollection DbParameterCollection { get { return _command.Parameters; } }
        public override void Cancel() { _command.Cancel(); }
        public override void Prepare() { _command.Prepare(); }
        public override int ExecuteNonQuery() { throw new NotImplementedException(); }
        public new OneDbDataReader ExecuteReader()
        {
            return ExecuteDbDataReader(CommandBehavior.Default);
        }
        public void AddParameter(string name, object value)
        {
            if (_command is SqlCommand ms)
            {
                ms.Parameters.AddWithValue(name, value);
            }
            else if (_command is NpgsqlCommand pg)
            {
                pg.Parameters.AddWithValue(name, value);
            }
        }
        public override object ExecuteScalar()
        {
            ScriptDetails details = GetScriptDetails();

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
        protected override OneDbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            ScriptDetails details = GetScriptDetails();

            DbDataReader reader = _command.ExecuteReader(behavior);

            return new OneDbDataReader(in reader, details.Mappers);
        }
        private ScriptDetails GetScriptDetails()
        {
            if (!ScriptProcessor.TryTranspile(in _context, in _commandText, out ScriptDetails result, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _command.CommandText = result.SqlScript;

            //TODO: parameters ???

            return result;
        }
    }
}