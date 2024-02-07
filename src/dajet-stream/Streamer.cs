using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Scripting;
using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    internal sealed class Streamer : ProcessorBase
    {
        internal Streamer(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
            : base(in context, in statement, in parameters) { }
        public override void Synchronize() { /* do nothing */ }
        public override void Process()
        {
            if (_mode == StatementType.Streaming)
            {
                StreamIntoObjectVariable();
            }
        }
        private void StreamIntoObjectVariable()
        {
            DataObject buffer = new(_statement.Mapper.Properties.Count);

            using (OneDbConnection connection = new(_context))
            {
                connection.Open();

                OneDbCommand command = connection.CreateCommand();
                DbTransaction transaction = connection.BeginTransaction();

                command.Transaction = transaction;
                command.CommandText = _statement.Script;

                ConfigureParameters(in command);

                try
                {
                    foreach (IDataReader reader in command.ExecuteNoMagic())
                    {
                        _statement.Mapper.Map(in reader, in buffer);

                        _parameters[_objectName] = buffer;

                        _next?.Process();
                    }
                    _next?.Synchronize();
                    transaction.Commit();
                }
                catch (Exception error)
                {
                    try
                    {
                        transaction.Rollback(); throw;
                    }
                    catch
                    {
                        throw error;
                    }
                }
                finally // clear streaming buffer
                {
                    _parameters[_objectName] = null;
                }
            }
        }
    }
}