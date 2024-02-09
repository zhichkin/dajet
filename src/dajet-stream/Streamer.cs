using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
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
                if (_statement.Node is ConsumeStatement)
                {
                    ConsumeIntoObjectVariable();
                }
                else // SELECT или UPDATE
                {
                    StreamIntoObjectVariable();
                }
            }
        }
        private void ConsumeIntoObjectVariable()
        {
            DataObject buffer = new(_statement.Mapper.Properties.Count);

            int consumed;

            do
            {
                consumed = 0;

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

                            consumed++;
                        }
                        
                        if (consumed > 0) { _next?.Synchronize(); }

                        transaction.Commit();
                    }
                    catch (Exception error)
                    {
                        try { transaction.Rollback(); throw; }
                        catch { throw error; }
                    }
                    finally // clear streaming buffer
                    {
                        _parameters[_objectName] = null;
                    }
                }

                Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} consumed {consumed} rows");
            }
            while (consumed > 0); // read while queue is not empty
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
                    int consumed = 0;

                    foreach (IDataReader reader in command.ExecuteNoMagic())
                    {
                        _statement.Mapper.Map(in reader, in buffer);

                        _parameters[_objectName] = buffer;

                        _next?.Process();

                        consumed++;
                    }
                    
                    _next?.Synchronize();
                    transaction.Commit();

                    Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} consumed {consumed} rows");

                    //TODO: consume while not empty !?
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