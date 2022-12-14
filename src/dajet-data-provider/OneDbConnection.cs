using DaJet.Metadata;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;
using System.Data.Common;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

namespace DaJet.Data.Provider
{
    public sealed class OneDbConnection : DbConnection
    {
        private readonly DbConnection _connection;

        private readonly string IB_KEY;
        private MetadataCache _metadata;
        private DatabaseProvider _provider;
        private readonly string _connectionString;
        public OneDbConnection(MetadataCache metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

            _provider = _metadata.DatabaseProvider;
            _connectionString = _metadata.ConnectionString;

            if (_provider == DatabaseProvider.PostgreSql)
            {
                _connection = new NpgsqlConnection(_connectionString);
            }
            else
            {
                _connection = new SqlConnection(_connectionString);
            }

            //TODO: ib key generation algorithm !?
            IB_KEY = _connection.Database;
        }
        public OneDbConnection(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _connectionString = connectionString;

            if (connectionString.StartsWith("Host"))
            {
                _provider = DatabaseProvider.PostgreSql;
                _connection = new NpgsqlConnection(_connectionString);
            }
            else
            {
                _provider = DatabaseProvider.SqlServer;
                _connection = new SqlConnection(_connectionString);
            }

            //TODO: ib key generation algorithm !?
            IB_KEY = _connection.Database;
        }
        private void InitializeMetadataCache()
        {
            if (_metadata != null) { return; }

            if (MetadataSingleton.Instance.TryGetMetadataCache(IB_KEY, out _metadata, out string error))
            {
                return;
            }

            InfoBaseOptions options = new()
            {
                Key = IB_KEY,
                DatabaseProvider = _provider,
                ConnectionString = _connectionString
            };
            
            MetadataSingleton.Instance.Add(options);

            if (!MetadataSingleton.Instance.TryGetMetadataCache(IB_KEY, out _metadata, out error))
            {
                throw new InvalidOperationException($"Metadata cache error: {error}");
            }
        }
        public override string ConnectionString
        {
            get { return _connection.ConnectionString; }
            set { _connection.ConnectionString = value; }
        }
        public override string Database { get { return _connection.Database; } }
        public override string DataSource { get { return _connection.DataSource; } }
        public override string ServerVersion { get { return _connection.ServerVersion; } }
        public override ConnectionState State { get { return _connection.State; } }
        public override void ChangeDatabase(string databaseName)
        {
            _connection.ChangeDatabase(databaseName);
        }
        public override void Open()
        {
            _connection.Open();

            InitializeMetadataCache();
        }
        public override void Close()
        {
            _connection.Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connection.Dispose();
            }

            _metadata = null!;
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            return _connection.BeginTransaction(isolationLevel);
        }
        public override void EnlistTransaction(Transaction? transaction)
        {
            _connection.EnlistTransaction(transaction);
        }
        //public override DataTable GetSchema()
        //{
        //    return base.GetSchema();
        //}
        protected override DbCommand CreateDbCommand()
        {
            return CreateCommand();
        }
        public new OneDbCommand CreateCommand()
        {
            return new OneDbCommand(_metadata) { Connection = _connection };
        }
    }
}