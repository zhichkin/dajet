using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    public sealed class ConsumeProcessor : OneDbProcessor
    {
        public ConsumeProcessor(in StreamScope scope) : base(in scope)
        {
            _next = StreamFactory.CreateStream(in _scope);
        }
        public override void Process()
        {
            while (true)
            {
                try
                {
                    Consume();
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
                }

                try
                {
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait(); // pool database queue each 1 second
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - host shutdown requested
                }
            }
        }
        private void Consume()
        {
            int consumed;
            int processed = 0;

            do
            {
                consumed = 0;

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

                                    _next?.Process();

                                    consumed++;
                                    processed++;
                                }
                                reader.Close();
                            }

                            if (consumed > 0)
                            {
                                _next?.Synchronize();
                            }

                            transaction.Commit();

                            if (consumed > 0)
                            {
                                FileLogger.Default.Write($"Consumed {consumed} messages");
                            }
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
            while (consumed > 0); // consume while queue is not empty
        }
    }
}