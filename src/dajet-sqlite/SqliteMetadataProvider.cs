using DaJet.Data;
using DaJet.Data.Sqlite;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.Linq;

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

            EntityRecord entity;
            NamespaceRecord parent;

            string[] identifiers = metadataName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (identifiers.Length == 1)
            {
                entity = source.Select<EntityRecord>(identifiers[0]);
            }
            else
            {
                parent = source.Select<NamespaceRecord>(identifiers[0]);

                if (parent is null) { return null; }

                var list = source.Query<EntityRecord>(parent.GetEntity());

                if (list is not List<EntityRecord> entities)
                {
                    return null;
                }

                entity = entities.Where(e => e.Name == identifiers[1]).FirstOrDefault();
            }

            if (entity is null) { return null; }

            return new SqliteDbConfigurator(in _connectionString).Convert(in entity);
        }
    }
}