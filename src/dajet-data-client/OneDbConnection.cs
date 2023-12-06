using DaJet.Metadata;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;
using System.Data.Common;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

namespace DaJet.Data.Client
{
    public sealed class OneDbConnection : DbConnection
    {
        private readonly DbConnection _connection;
        private readonly string _connectionString;
        private readonly DatabaseProvider _provider;
        private readonly IMetadataProvider _context;
        public OneDbConnection(IMetadataProvider context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _provider = _context.DatabaseProvider;
            _connectionString = _context.ConnectionString;

            _connection = CreateDbConnection();
        }
        private DbConnection CreateDbConnection()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return new SqlConnection(_connectionString);
            }
            else if (_provider == DatabaseProvider.PostgreSql)
            {
                return new NpgsqlConnection(_connectionString);
            }

            throw new InvalidOperationException($"Unsupported database provider: {_provider}");
        }
        public new OneDbCommand CreateCommand() { return new OneDbCommand(_context, _connection); }

        #region "ABSTRACT BASE CLASS IMPLEMENTATION"
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
        public override void Open() { _connection.Open(); }
        public override void Close() { _connection.Close(); }
        protected override void Dispose(bool disposing) { if (disposing) { _connection.Dispose(); } }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            return _connection.BeginTransaction(isolationLevel);
        }
        public override void EnlistTransaction(Transaction transaction)
        {
            _connection.EnlistTransaction(transaction);
        }
        //public override DataTable GetSchema()
        //{
        //    return base.GetSchema();
        //}
        protected override DbCommand CreateDbCommand() { return CreateCommand(); }
        #endregion
    }
}