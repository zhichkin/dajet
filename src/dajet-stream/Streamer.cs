using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Scripting;
using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    internal sealed class Streamer : ProcessorBase
    {
        internal Streamer(in Pipeline pipeline, in SqlStatement statement) : base(pipeline, statement) { }
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

            using (OneDbConnection connection = new(_pipeline.Context))
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

                        _pipeline.Parameters[_objectName] = buffer;

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
            }
        }
    }
}