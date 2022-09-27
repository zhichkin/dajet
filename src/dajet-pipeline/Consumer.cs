using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace DaJet.Pipeline.SqlServer
{
    public sealed class Consumer<TMessage> : Source<TMessage>, IConfigurable where TMessage : class, new()
    {
        private readonly IPipeline _pipeline;
        private Dictionary<string, string> _options;
        public Consumer(IPipeline pipeline)
        {
            _pipeline = pipeline;
        }
        public void Configure(Dictionary<string, string> options)
        {
            _options = options;

            //if (options.TryGetValue("$ref", out string? key) && !string.IsNullOrWhiteSpace(key))
            //{
            //    _options.Name = key;

            //    IDatabaseRegistry registry = _serviceProvider.GetRequiredService<IDatabaseRegistry>();

            //    if (registry.Databases.TryGetValue(key, out DatabaseInfo? database) && database != null)
            //    {
            //        // apply general options
            //        _options.DatabaseProvider = database.Type;
            //        DatabaseOptions.Configure(in _options, database.Options);
            //    }
            //}

            //// apply individual options
            //DatabaseOptions.Configure(in _options, options);

            //_mapper = _mapperFactory.CreateDataMapper<TMessage>(_options);
        }
        public override void Pump(CancellationToken token)
        {
            TMessage _message = new(); // buffer
            
            int consumed;
            
            using (SqlConnection connection = new(_options["ConnectionString"]))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    //_mapper.ConfigureSelect(command);

                    do
                    {
                        consumed = 0;

                        Stopwatch watch = new();
                        watch.Start();

                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            command.Transaction = transaction;

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    consumed++;

                                    //_mapper.MapDataToMessage(reader, in _message);
                                    
                                    _Process(in _message);
                                }
                                reader.Close();
                            }

                            if (consumed > 0)
                            {
                                _Synchronize();

                                transaction.Commit();
                            }
                        }

                        watch.Stop();
                        
                        //_logger?.LogInformation($"[{_pipelineName}] Consumed {consumed} messages in {watch.ElapsedMilliseconds} milliseconds.");
                    }
                    while (consumed > 0 && !token.IsCancellationRequested);
                }
            }
        }
    }
}