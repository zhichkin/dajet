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

        private readonly string IB_KEY;
        private DatabaseProvider _provider;
        private IMetadataProvider _context;
        private readonly string _connectionString;
        public OneDbConnection(IMetadataProvider context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _provider = _context.DatabaseProvider;
            _connectionString = _context.ConnectionString;

            _connection = CreateDbConnection();

            //TODO: ib key generation algorithm !?
            IB_KEY = _connection.Database;
        }
        public OneDbConnection(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _connectionString = connectionString;

            if (connectionString.StartsWith("Host"))
            {
                _provider = DatabaseProvider.PostgreSql;
            }
            else
            {
                _provider = DatabaseProvider.SqlServer;
            }

            _connection = CreateDbConnection();

            //TODO: ib key generation algorithm !?
            IB_KEY = _connection.Database;
        }
        private DbConnection CreateDbConnection()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return new SqlConnection(_connectionString);
            }
            else if(_provider == DatabaseProvider.PostgreSql)
            {
                return new NpgsqlConnection(_connectionString);
            }

            throw new InvalidOperationException($"Unsupported database provider: {_provider}");
        }
        private void InitializeMetadataCache()
        {
            if (_context is not null) { return; }

            if (MetadataSingleton.Instance.TryGetMetadataCache(IB_KEY, out MetadataCache cache, out string error))
            {
                _context = cache; return;
            }

            InfoBaseOptions options = new()
            {
                Key = IB_KEY,
                DatabaseProvider = _provider,
                ConnectionString = _connectionString
            };
            
            MetadataSingleton.Instance.Add(options);

            if (MetadataSingleton.Instance.TryGetMetadataCache(IB_KEY, out cache, out error))
            {
                _context = cache;
            }
            else
            {
                throw new InvalidOperationException($"Metadata cache error: {error}");
            }
        }

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

            _context = null!;
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
            return new OneDbCommand(_context) { Connection = _connection };
        }
        #endregion

        public DataObject GetDataObject(Entity entity)
        {
            DataObject root = null;

            ScriptDetails details = ScriptGenerator.GenerateSelectEntityScript(in _context, entity);

            using (DbConnection connection = CreateDbConnection())
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = details.SqlScript;

                    if (command is SqlCommand ms)
                    {
                        foreach (var parameter in details.Parameters)
                        {
                            ms.Parameters.AddWithValue(parameter.Key, parameter.Value);
                        }
                    }
                    else if (command is NpgsqlCommand pg)
                    {
                        foreach (var parameter in details.Parameters)
                        {
                            pg.Parameters.AddWithValue(parameter.Key, parameter.Value);
                        }
                    }

                    int mapper = 0;
                    int capacity;

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            capacity = details.Mappers[mapper].Properties.Count;

                            root = new DataObject(capacity);

                            details.Mappers[mapper].Map(in reader, in root);
                        }

                        while (reader.NextResult())
                        {
                            ++mapper;

                            capacity = details.Mappers[mapper].Properties.Count;

                            List<DataObject> table = new();

                            while (reader.Read())
                            {
                                DataObject record = new(capacity);

                                details.Mappers[mapper].Map(in reader, in record);
                                
                                table.Add(record);
                            }

                            root.SetValue(details.Mappers[mapper].Name, table);
                        }
                        
                        reader.Close();
                    }
                }
            }

            return root;
        }
    }
}