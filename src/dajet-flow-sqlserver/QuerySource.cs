using DaJet.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Flow.SqlServer
{
    [PipelineBlock] public sealed class QuerySource : SourceBlock<Dictionary<string, object>>
    {
        private readonly IPipelineManager _manager;
        private readonly InfoBaseDataMapper _databases;
        private string ConnectionString { get; set; }
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [ActivatorUtilitiesConstructor] public QuerySource(InfoBaseDataMapper databases, IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        protected override void _Configure()
        {
            InfoBaseModel database = _databases.Select(Source) ?? throw new Exception($"Source not found: {Source}");
            
            ConnectionString = database.ConnectionString;
        }
        public override void Execute()
        {
            int consumed;

            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    ConfigureCommand(in command);

                    consumed = 0;

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        command.Transaction = transaction;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ProcessDataRecord(in reader); consumed++;
                            }
                            reader.Close();
                        }

                        if (consumed > 0)
                        {
                            _next?.Synchronize();
                            transaction.Commit();
                        }
                    }
                }
            }

            _manager.UpdatePipelineStatus(Pipeline, $"Processed {consumed} messages");
        }
        private void ConfigureCommand(in SqlCommand command)
        {
            command.CommandText = Script;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 60; // seconds
        }
        private void ProcessDataRecord(in SqlDataReader reader)
        {
            Dictionary<string, object> record = new();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                record.Add(reader.GetName(i), reader.GetValue(i));
            }
            
            _next?.Process(in record);
        }
    }
}