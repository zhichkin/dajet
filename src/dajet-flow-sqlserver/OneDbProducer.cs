using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Text;

namespace DaJet.Flow.SqlServer
{
    [PipelineBlock] public sealed class OneDbProducer : TargetBlock<IDataRecord>
    {
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly IMetadataService _metadata;
        [Option] public string Target { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbProducer(InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
        {
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        protected override void _Configure()
        {
            if (Timeout < 0) { Timeout = 10; }

            InfoBaseRecord database = _databases.Select(Target) ?? throw new ArgumentException($"Target not found: {Target}");
            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script) ?? throw new ArgumentException($"Script not found: {Script}");

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }
            
            ConnectionString = database.ConnectionString;

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
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
        private void ConfigureParameters(in SqlCommand command, in IDataRecord input)
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
                else if (value is bool boolean)
                {
                    command.Parameters.AddWithValue(input.GetName(i), boolean ? new byte[1] { 1 } : new byte[1] { 0 });
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