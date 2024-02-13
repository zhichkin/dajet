using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DaJet.Stream
{
    internal sealed class Streamer : ProcessorBase
    {
        internal Streamer(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
            : base(in context, in statement, in parameters) { }
        public override void Synchronize() { _next?.Synchronize(); }
        public override void Process()
        {
            Stopwatch watch = new();

            watch.Start();

            int tid = Environment.CurrentManagedThreadId;

            int processed = 0;

            try
            {
                if (_connection.State != ConnectionState.Open)
                {
                    _connection = new OneDbConnection(_context);

                    _connection.Open();
                }

                if (_mode == StatementType.Streaming)
                {
                    if (_statement.Node is ConsumeStatement)
                    {
                        processed = ConsumeIntoObjectVariable();
                    }
                    else // SELECT или UPDATE
                    {
                        processed = StreamIntoObjectVariable();
                    }
                }

            }
            catch (Exception error)
            {
                _connection.Dispose();

                Console.WriteLine(ExceptionHelper.GetErrorMessage(error));
            }

            watch.Stop();

            long elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Thread {tid} processed {processed} rows in {elapsed} ms");
        }
        private int ConsumeIntoObjectVariable()
        {
            DataObject buffer = new(_statement.Mapper.Properties.Count);

            int consumed;
            int processed = 0;

            do
            {
                Stopwatch watch = new();

                watch.Start();

                consumed = 0;

                OneDbCommand command = _connection.CreateCommand();
                DbTransaction transaction = _connection.BeginTransaction();
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
                        processed++;
                    }

                    _next?.Synchronize();
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

                watch.Stop();

                long elapsed = watch.ElapsedMilliseconds;

                Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} consumed {consumed} rows in {elapsed} ms");
            }
            while (consumed > 0); // read while queue is not empty

            return processed;
        }
        private int StreamIntoObjectVariable()
        {
            DataObject buffer = new(_statement.Mapper.Properties.Count);

            int processed = 0;

            OneDbCommand command = _connection.CreateCommand();
            DbTransaction transaction = _connection.BeginTransaction();

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

                    processed++;
                }

                _next?.Synchronize();
                transaction.Commit();

                Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} processed {processed} rows");
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

            return processed;
        }
    }
}