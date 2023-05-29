using Npgsql;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;

namespace DaJet.Data.PostgreSql
{
    public sealed class PgQueryExecutor : QueryExecutor
    {
        private readonly NpgsqlDataSource _source;
        public PgQueryExecutor(string connectionString) : base(connectionString)
        {
            PgDbConfigurator.InitializeUserDefinedTypes(in _connectionString);

            NpgsqlDataSourceBuilder builder = new(_connectionString);

            string database = new NpgsqlConnectionStringBuilder(_connectionString).Database;

            Dictionary<string, Type> types = PgDbConfigurator.GetUserDefinedTypes(in database);

            if (types is not null)
            {
                foreach (var item in types)
                {
                    builder.MapComposite(item.Value, item.Key);
                }
            }
            
            _source = builder.Build();
        }
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
                    object records = CreateTableParameter(in tvp);

                    _ = _command.Parameters.AddWithValue(tvp.Name, records);
                }
                else
                {
                    _ = _command.Parameters.AddWithValue(item.Key, item.Value);
                }
            }
        }
        private object CreateTableParameter(in TableValuedParameter tvp)
        {
            Type type = PgDbConfigurator.GetUserDefinedType(GetDatabaseName(), tvp.DbName);

            if (type is null)
            {
                throw new InvalidOperationException($"Type [{tvp.DbName}] is not found.");
            }

            Type list = typeof(List<>).MakeGenericType(new Type[] { type });
            
            object records = Activator.CreateInstance(list);

            if (records is not IList result)
            {
                throw new InvalidOperationException($"Failed to create instance of [{list}]");
            }

            foreach (var record in tvp.Value)
            {
                object row = Activator.CreateInstance(type);

                int index = 0;

                foreach (var column in record)
                {
                    PropertyInfo property = type.GetProperty(column.Key);

                    if (property is null)
                    {
                        throw new InvalidOperationException($"Property [{column.Key}] is not found.");
                    }

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

                result.Add(row);
            }

            return result;
        }
    }
}