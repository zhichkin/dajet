using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    public sealed class ConsumeProcessor : OneDbProcessor
    {
        private int _state;
        private const int STATE_IDLE = 0;
        private const int STATE_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_IDLE) == STATE_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_ACTIVE) == STATE_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_ACTIVE) == STATE_ACTIVE; } }

        private AutoResetEvent _sleep;
        public ConsumeProcessor(in StreamScope scope) : base(in scope)
        {
            _next = StreamFactory.CreateStream(in _scope);

            if (_next is null)
            {
                throw new InvalidOperationException($"[{nameof(ConsumeProcessor)}] Continuation processor is missing!");
            }
        }
        public override void Process()
        {
            if (CanExecute) // STATE_IDLE -> STATE_ACTIVE
            {
                AutoResetEvent sleep = new(false);

                if (Interlocked.CompareExchange(ref _sleep, sleep, null) is not null)
                {
                    sleep.Dispose();
                }

                WhileActiveDoWork(); // STATE_ACTIVE -> STATE_IDLE
            }
        }
        private void WhileActiveDoWork()
        {
            while (IsActive) // STATE_ACTIVE
            {
                int delay = 1;

                try
                {
                    int processed = Consume();

                    FileLogger.Default.Write($"Processed {processed} messages");
                }
                catch (Exception error)
                {
                    delay = 60;

                    FileLogger.Default.Write(error);
                }
                finally
                {
                    _next.Dispose(); //NOTE: continuation processor must be reusable !!!
                }

                if (IsActive && _sleep is not null)
                {
                    FileLogger.Default.Write($"Sleep {delay} seconds");

                    bool signaled = _sleep.WaitOne(TimeSpan.FromSeconds(delay)); // suspend thread

                    if (signaled) // the Dispose method is called -> STATE_IDLE
                    {
                        FileLogger.Default.Write($"Shutdown requested");
                    }
                }
            }
        }
        private int Consume()
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

                                    consumed++;

                                    _next.Process(); //NOTE: continuation processor must be reusable !!!

                                    processed++;
                                }
                                reader.Close();
                            }

                            if (consumed > 0) { _next.Synchronize(); } // submit batch

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

                if (consumed > 0)
                {
                    FileLogger.Default.Write($"Consumed {consumed} messages");
                }
            }
            while (consumed > 0 && IsActive); // consume while queue is not empty or Dispose is not called

            return processed;
        }

        public override void Dispose()
        {
            if (CanDispose) // STATE_ACTIVE -> STATE_DISPOSING
            {
                try
                {
                    _next.Dispose(); //NOTE: continuation processor must be reusable !!!
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(error);
                }

                try
                {
                    _ = _sleep.Set(); // release thread if suspended
                    _sleep.Dispose();
                }
                finally
                {
                    _sleep = null;
                }

                _ = Interlocked.Exchange(ref _state, STATE_IDLE);
            }
        }
    }
}