﻿using DaJet.Data;
using DaJet.Options;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

namespace DaJet.Flow.PostgreSql
{
    [PipelineBlock] public sealed class Producer : TargetBlock<Dictionary<string, object>>
    {
        private readonly InfoBaseDataMapper _databases;
        private string ConnectionString { get; set; }
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Target { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [ActivatorUtilitiesConstructor] public Producer(InfoBaseDataMapper databases)
        {
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        protected override void _Configure()
        {
            InfoBaseModel database = _databases.Select(Target);
            if (database is null) { throw new Exception($"Target not found: {Target}"); }

            ConnectionString = database.ConnectionString;
        }
        public override void Process(in Dictionary<string, object> input)
        {
            foreach (var item in input)
            {
                if (item.Value is Entity value)
                {
                    input[item.Key] = value.Identity;
                }
            }

            using (NpgsqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    ConfigureCommand(command, in input);

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        private void ConfigureCommand(in NpgsqlCommand command, in Dictionary<string, object> input)
        {
            command.CommandType = CommandType.Text;
            command.CommandText = Script;
            command.CommandTimeout = 10; // seconds

            command.Parameters.Clear();

            foreach (var parameter in input)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            }
        }
    }
}