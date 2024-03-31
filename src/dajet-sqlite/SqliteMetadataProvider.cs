using DaJet.Data;
using DaJet.Data.Sqlite;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Sqlite
{
    public sealed class SqliteMetadataProvider : IMetadataProvider
    {
        private readonly string _connectionString;
        public SqliteMetadataProvider(in string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _connectionString = connectionString;
        }
        public int YearOffset { get { return 0; } }
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseProvider DatabaseProvider { get { return DatabaseProvider.Sqlite; } }
        public IQueryExecutor CreateQueryExecutor() { return new SqliteQueryExecutor(in _connectionString); }
        public IDbConfigurator GetDbConfigurator() { return new SqliteDbConfigurator(in _connectionString); }
        public MetadataItem GetMetadataItem(int typeCode) { throw new NotImplementedException(); }
        public IEnumerable<MetadataItem> GetMetadataItems(Guid type) { throw new NotImplementedException(); }
        public MetadataObject GetMetadataObject(Guid type, Guid uuid) { throw new NotImplementedException(); }
        public bool TryGetExtendedInfo(Guid uuid, out MetadataItemEx info) { throw new NotImplementedException(); }
        public bool TryGetEnumValue(in string identifier, out EnumValue value)
        {
            value = null;
            return false;
        }
        public MetadataObject GetMetadataObject(string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            MetadataSource source = new(in _connectionString);

            string[] identifiers = metadataName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            int current = 0;
            EntityRecord entity = null;
            NamespaceRecord _namespace;
            Entity parent = Entity.Undefined;

            while (current < identifiers.Length)
            {
                _namespace = source.Select<NamespaceRecord>(parent, identifiers[current]);

                if (_namespace is not null)
                {
                    parent = _namespace.GetEntity(); current++; continue;
                }

                entity = source.Select<EntityRecord>(parent, identifiers[current]);

                if (entity is not null)
                {
                    parent = entity.GetEntity(); current++; continue;
                }

                break; // not found
            }

            if (entity is null) { return null; }

            return new SqliteDbConfigurator(in _connectionString).Convert(in entity);
        }
    }
}