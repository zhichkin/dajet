using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class StreamProcessor : OneDbProcessor
    {
        private readonly IProcessor _append;
        public StreamProcessor(in ScriptScope scope) : base(in scope)
        {
            _append = StreamFactory.CreateAppendStream(in scope, in _into);
        }
        public override void Process()
        {
            try
            {
                int streamed = Stream();

                FileLogger.Default.Write($"Streamed {streamed} messages");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(error);
            }
            finally
            {
                _next?.Dispose();
            }
        }
        private int Stream()
        {
            int streamed = 0;

            using (DbConnection connection = _factory.Create(in _uri))
            {
                connection.Open();

                DbTransaction transaction = connection.BeginTransaction();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.Connection = connection;
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = _statement.Script;

                    InitializeParameterValues();

                    _factory.ConfigureParameters(in command, in _parameters, _yearOffset);

                    DataObject record = new(_statement.Mapper.Properties.Count);

                    try
                    {
                        using (DbDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _statement.Mapper.Map(reader, in record);

                                _ = _scope.TrySetValue(_into.Identifier, record);

                                _append?.Process();

                                _next?.Process();

                                streamed++;
                            }
                            reader.Close();
                        }

                        if (streamed > 0) { _next?.Synchronize(); } //TODO: submit batches !?

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
                        _ = _scope.TrySetValue(_into.Identifier, null);
                    }
                }
            }

            return streamed;
        }
    }
}