using DaJet.Data;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace DaJet.Metadata.SqlServer
{
    public sealed class MsMetadataProvider : IMetadataProvider
    {
        private string _connectionString;
        public void Configure(in Dictionary<string, string> options)
        {
            if (!options.TryGetValue(nameof(InfoBaseOptions.ConnectionString), out string connectionString))
            {
                throw new InvalidOperationException();
            }

            SqlConnectionStringBuilder builder = new(connectionString);

            _connectionString = builder.ToString();
        }

        public int YearOffset { get { return 0; } }
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseProvider DatabaseProvider { get { return DatabaseProvider.SqlServer; } }
        public IQueryExecutor CreateQueryExecutor() { return new MsQueryExecutor(_connectionString); }
        public bool IsRegularDatabase
        {
            get
            {
                IQueryExecutor executor = CreateQueryExecutor();
                string script = SQLHelper.GetTableExistsScript("_yearoffset");
                return !(executor.ExecuteScalar<int>(in script, 10) == 1);
            }
        }
        public IEnumerable<MetadataItem> GetMetadataItems(Guid type)
        {
            IQueryExecutor executor = QueryExecutor.Create(DatabaseProvider.SqlServer, _connectionString);

            string script = SQLHelper.GetTableSelectScript();

            List<MetadataItem> list = new();

            foreach (IDataReader reader in executor.ExecuteReader(script, 10))
            {
                list.Add(new MetadataItem(Guid.Empty, Guid.Empty, reader.GetString(0)));
            }

            return list;
        }
        public bool TryGetEnumValue(in string identifier, out EnumValue value)
        {
            throw new NotImplementedException();
        }
        public bool TryGetExtendedInfo(Guid uuid, out MetadataItemEx info)
        {
            info = MetadataItemEx.Empty; return false;
        }

        public IDbConfigurator GetDbConfigurator()
        {
            return new MsDbConfigurator(this);
        }
        public MetadataItem GetMetadataItem(int typeCode)
        {
            throw new NotImplementedException();
        }
        public MetadataObject GetMetadataObject(string metadataName)
        {
            MsDbConfigurator configurator = new(this);

            return configurator.GetTypeDefinition(in metadataName);
        }
    }
}