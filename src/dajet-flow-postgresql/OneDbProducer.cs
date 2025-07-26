using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;
using System.Text;

namespace DaJet.Flow.PostgreSql
{
    public sealed class OneDbProducer : TargetBlock<DataObject>
    {
        private readonly IDataSource _source;
        private readonly ProducerOptions _options;
        private readonly IMetadataService _metadata;
        [ActivatorUtilitiesConstructor]
        public OneDbProducer(IDataSource source, ProducerOptions options, IMetadataService metadata)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

            Configure();
        }
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        private void Configure()
        {
            if (_options.Timeout < 0) { _options.Timeout = 10; }

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(_options.Target)
                ?? throw new ArgumentException($"Target not found: {_options.Target}");

            string scriptPath = database.Name + "/" + _options.Script;

            ScriptRecord script = _source.Select<ScriptRecord>(scriptPath)
                ?? throw new Exception($"Script not found: {scriptPath}");

            if (!_metadata.TryGetOrCreate(in database, out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ConnectionString = database.ConnectionString;

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in provider, script.Script, in parameters, out TranspilerResult result, out error))
            {
                throw new Exception(error);
            }

            ScriptParameters = result.Parameters;
            CommandText = GetInsertStatementScript(result.Statements);

            for (int i = 0; i < result.Statements.Count; i++)
            {
                if (result.Statements[i].Node is CreateSequenceStatement) // configure sequence database object
                {
                    provider.CreateQueryExecutor().ExecuteNonQuery(result.Statements[i].Script, 10); break;
                }
            }
        }
        private string GetInsertStatementScript(in List<SqlStatement> statements)
        {
            StringBuilder script = new();

            for (int i = 0; i < statements.Count; i++)
            {
                SqlStatement command = statements[i];

                if (string.IsNullOrEmpty(command.Script))
                {
                    continue;
                }

                if (command.Node is InsertStatement)
                {
                    script.AppendLine(command.Script); break;
                }
            }

            return script.ToString();
        }
        public override void Process(in DataObject input)
        {
            using (NpgsqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                NpgsqlCommand command = connection.CreateCommand();
                NpgsqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = CommandText;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = _options.Timeout;

                try
                {
                    ConfigureParameters(in command, in input);

                    _ = command.ExecuteNonQuery();

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
        }
        private void ConfigureParameters(in NpgsqlCommand command, in DataObject input)
        {
            command.Parameters.Clear();

            string name;
            object value;

            for (int i = 0; i < input.Count(); i++)
            {
                name = input.GetName(i);
                value = input.GetValue(i);

                if (!ScriptParameters.ContainsKey(name)) { continue; }

                if (value is null)
                {
                    command.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (value is Entity entity)
                {
                    command.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else if (value is Guid uuid)
                {
                    command.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else
                {
                    command.Parameters.AddWithValue(name, value);
                }
            }
        }
    }
}