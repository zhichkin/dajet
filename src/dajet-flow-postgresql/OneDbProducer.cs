﻿using DaJet.Data;
using DaJet.Metadata;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

namespace DaJet.Flow.PostgreSql
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
        private GeneratorResult ScriptGenerator { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        protected override void _Configure()
        {
            InfoBaseModel database = _databases.Select(Target) ?? throw new ArgumentException($"Target not found: {Target}");
            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script) ?? throw new ArgumentException($"Script not found: {Script}");

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            ScriptGenerator = executor.PrepareScript(script.Script);
            ScriptParameters = executor.Parameters;

            if (!ScriptGenerator.Success)
            {
                throw new Exception(ScriptGenerator.Error);
            }

            CommandText = ScriptGenerator.Script;
            ConnectionString = database.ConnectionString;

            if (Timeout < 0) { Timeout = 10; }
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
                else
                {
                    command.Parameters.AddWithValue(input.GetName(i), value);
                }
            }
        }
    }
}