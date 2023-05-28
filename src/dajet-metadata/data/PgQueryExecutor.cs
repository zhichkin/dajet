using DaJet.Metadata;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;

namespace DaJet.Data.PostgreSql
{
    public sealed class PgQueryExecutor : QueryExecutor
    {
        private readonly NpgsqlDataSource _source;
        private readonly IMetadataProvider _metadata;
        private readonly PgDbConfigurator _configurator;
        public PgQueryExecutor(string connectionString) : base(connectionString)
        {
            _source = new NpgsqlDataSourceBuilder(_connectionString).Build();
        }
        //public PgQueryExecutor(IMetadataProvider provider) : base(provider.ConnectionString)
        //{
        //    _metadata = provider;
        //    _configurator = new(_metadata);
        //    _configurator.InitializeUserDefinedTypes();

        //    NpgsqlDataSourceBuilder builder = new(_connectionString);

        //    foreach (EntityDefinition definition in _configurator.SelectTypeDefinitions())
        //    {
        //        Type type = _configurator.GetUserDefinedType(definition.Name);

        //        _ = builder.MapComposite(type, definition.Name);
        //    }

        //    _source = builder.Build();
        //}
        public override string GetDatabaseName()
        {
            string databaseName = new NpgsqlConnectionStringBuilder(_connectionString).Database;

            return (string.IsNullOrWhiteSpace(databaseName) ? string.Empty : databaseName);
        }
        protected override DbConnection GetDbConnection()
        {
            return _source.CreateConnection();
        }
        protected override void ConfigureQueryParameters(in DbCommand command, in Dictionary<string, object> parameters)
        {
            if (command is not NpgsqlCommand _command)
            {
                throw new InvalidOperationException(nameof(command));
            }

            foreach (var item in parameters)
            {
                if (item.Value is TableValuedParameter tvp)
                {
                    List<object> records = CreateTableParameter(in tvp);

                    _ = _command.Parameters.AddWithValue(tvp.Name, records);
                }
                else
                {
                    _ = _command.Parameters.AddWithValue(item.Key, item.Value);
                }
            }
        }
        private List<object> CreateTableParameter(in TableValuedParameter tvp)
        {
            List<object> records = new();

            Type type = _configurator.GetUserDefinedType(tvp.DbName);

            foreach (var record in tvp.Value)
            {
                object row = Activator.CreateInstance(type);

                int index = 0;

                foreach (var column in record)
                {
                    PropertyInfo property = type.GetProperty(column.Key);

                    if (column.Value is null) { throw new InvalidOperationException("NULL values is not allowed"); }
                    else if (column.Value is bool boolean) { property.SetValue(row, boolean); }
                    else if (column.Value is int integer) { property.SetValue(row, integer); }
                    else if (column.Value is decimal numeric) { property.SetValue(row, numeric); }
                    else if (column.Value is DateTime datetime) { property.SetValue(row, datetime); }
                    else if (column.Value is string text) { property.SetValue(row, text); }
                    else if (column.Value is byte[] binary) { property.SetValue(row, binary); }
                    else if (column.Value is Guid uuid) { property.SetValue(row, uuid.ToByteArray()); }
                    else if (column.Value is Entity entity) { property.SetValue(row, entity.Identity.ToByteArray()); }
                    else { throw new InvalidOperationException("Unsupported data type"); }

                    index++;
                }

                records.Add(row);
            }

            return records;
        }
    }
}