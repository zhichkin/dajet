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
            int processed = 0;

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
                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _statement.Mapper.Map(in reader, in record);

                                _ = _scope.TrySetValue(_into.Identifier, record);

                                _append?.Process();
                                
                                _next?.Process();

                                processed++;
                            }
                            reader.Close();
                        }
                        
                        if (processed > 0)
                        {
                            _next?.Synchronize();
                        }

                        transaction.Commit();

                        FileLogger.Default.Write($"[{Environment.CurrentManagedThreadId}] Streamed {processed} messages");
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
        }
    }
}