using DaJet.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Flow.SqlServer
{
    [PipelineBlock] public sealed class QuerySource : SourceBlock<IDataRecord>
    {
        private readonly IPipelineManager _manager;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public QuerySource(InfoBaseDataMapper databases, ScriptDataMapper scripts, IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        protected override void _Configure()
        {
            InfoBaseModel database = _databases.Select(Source) ?? throw new ArgumentException($"Source not found: {Source}");
            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script) ?? throw new ArgumentException($"Script not found: {Script}");

            CommandText = script.Script;
            ConnectionString = database.ConnectionString;

            if (Timeout < 0) { Timeout = 10; }
        }
        public override void Execute()
        {
            int consumed = 0;

            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = CommandText;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Timeout;

                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _next?.Process(reader); consumed++;
                        }
                        reader.Close();
                    }

                    if (consumed > 0)
                    {
                        _next?.Synchronize();
                    }

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

            _manager.UpdatePipelineStatus(Pipeline, $"Processed {consumed} records");
        }
    }
}