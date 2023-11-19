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
    public sealed class OneDbProducer : TargetBlock<IDataRecord>
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

            if (!_metadata.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ConnectionString = database.ConnectionString;

            ScriptExecutor executor = new(provider, _metadata, _source);
            executor.PrepareScript(script.Script, out ScriptModel _, out List<ScriptCommand> commands);
            ScriptParameters = executor.Parameters;
            CommandText = GetInsertStatementScript(in commands);

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Statement is CreateSequenceStatement) // configure sequence database object
                {
                    provider.CreateQueryExecutor().ExecuteNonQuery(commands[i].Script, 10); break;
                }
            }
        }
        private string GetInsertStatementScript(in List<ScriptCommand> commands)
        {
            StringBuilder script = new();

            for (int i = 0; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

                if (string.IsNullOrEmpty(command.Script))
                {
                    continue;
                }

                if (command.Statement is InsertStatement)
                {
                    script.AppendLine(command.Script); break;
                }
            }

            return script.ToString();
        }
        public override void Process(in IDataRecord input)
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
        private void ConfigureParameters(in NpgsqlCommand command, in IDataRecord input)
        {
            command.Parameters.Clear();

            string name;
            object value;

            for (int i = 0; i < input.FieldCount; i++)
            {
                name = input.GetName(i);
                value = input.GetValue(i);

                if (!ScriptParameters.ContainsKey(name)) { continue; }

                if (value is null)
                {
                    command.Parameters.AddWithValue(input.GetName(i), DBNull.Value);
                }
                else if (value is Entity entity)
                {
                    command.Parameters.AddWithValue(input.GetName(i), entity.Identity.ToByteArray());
                }
                else if (value is Guid uuid)
                {
                    command.Parameters.AddWithValue(input.GetName(i), uuid.ToByteArray());
                }
                else
                {
                    command.Parameters.AddWithValue(input.GetName(i), value);
                }
            }
        }
    }
}