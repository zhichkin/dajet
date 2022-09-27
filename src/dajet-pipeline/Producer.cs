using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace DaJet.Pipeline.SqlServer
{
    public sealed class Producer<TMessage> : Target<TMessage>, IConfigurable where TMessage : class, new()
    {
        private readonly IPipeline _pipeline;
        private Dictionary<string, string> _options;
        public Producer(IPipeline pipeline)
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
        protected override void _Process(in TMessage message)
        {
            using (SqlConnection connection = new(_options["ConnectionString"]))
            {
                connection.Open();

                Stopwatch watch = new();
                watch.Start();

                using (SqlCommand command = connection.CreateCommand())
                {
                    //_mapper.ConfigureInsert(command, in message);

                    _ = command.ExecuteNonQuery();
                }

                watch.Stop();

                //_logger?.LogInformation($"[{_pipelineName}] Produced {produced} messages in {watch.ElapsedMilliseconds} milliseconds.");
            }
        }
    }
}