using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Data.Common;
using IsolationLevel = System.Data.IsolationLevel;
using PropertyDefinition = DaJet.Scripting.Model.PropertyDefinition;
using TypeDefinition = DaJet.Scripting.Model.TypeDefinition;

namespace DaJet.Runtime
{
    public abstract class DbRequestProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly Uri _uri;
        private readonly IDbConnectionFactory _factory;
        private int _timeout = 30; // seconds
        private bool _stream = false;
        private IsolationLevel _transaction = IsolationLevel.Unspecified;
        private string _script = string.Empty;
        private string _output = string.Empty;
        private TokenType _type = TokenType.Ignore;
        private DataObject _record = null;
        private List<DataObject> _table = new();
        public DbRequestProcessor(in ScriptScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (Scope.Owner is not RequestStatement statement)
            {
                throw new ArgumentException(nameof(RequestStatement));
            }

            Statement = statement;

            ConfigureOutput(); // configure _schema by _output variable
            
            _uri = Scope.GetUri(Statement.Target);
            _factory = DbConnectionFactory.GetFactory(in _uri);

            ConfigureDataMapper(); // overridable
            ConfigureProcessor();  // overridable
        }
        public ScriptScope Scope { get; }
        public RequestStatement Statement { get; }
        public TypeDefinition DataSchema { get; set; }
        public IEntityMapper DataMapper { get; private set; }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        private void ConfigureOutput()
        {
            if (Statement.Response is not null)
            {
                _output = Statement.Response.Identifier;

                if (!Scope.TryGetDeclaration(_output, out _, out DeclareStatement declare))
                {
                    throw new InvalidOperationException();
                }

                if (declare.Type.Identifier == "object")
                {
                    _type = TokenType.Object;
                }
                else if (declare.Type.Identifier == "array")
                {
                    _type = TokenType.Array;
                }
                else
                {
                    throw new InvalidOperationException();
                }

                if (declare.TypeOf is null)
                {
                    throw new InvalidOperationException();
                }

                if (!Scope.TryGetDefinition(declare.TypeOf.Identifier, out TypeDefinition definition))
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    DataSchema = definition;
                }
            }
        }
        protected virtual void ConfigureProcessor()
        {
            Dictionary<string, string> headers = new(Statement.Headers.Count);

            foreach (ColumnExpression option in Statement.Headers)
            {
                if (!StreamFactory.TryEvaluate(Scope, option.Expression, out object value))
                {
                    throw new InvalidOperationException();
                }

                if (option.Alias == "Timeout" && value is int timeout)
                {
                    _timeout = timeout <= 0 ? 30 : timeout;
                }
                else if (option.Alias == "Script" && value is string script)
                {
                    if (script.StartsWith("file:"))
                    {
                        string scriptPath = UriHelper.GetScriptFilePath(script);
                        _script = UriHelper.GetScriptSourceCode(in scriptPath);
                    }
                    else
                    {
                        _script = script;
                    }
                }
                else if (option.Alias == "Stream" && value is bool stream)
                {
                    _stream = stream;
                }
                else if (option.Alias == "Transaction" && value is string transaction)
                {
                    if (!Enum.TryParse(transaction, out _transaction))
                    {
                        throw new ArgumentOutOfRangeException("[REQUEST] Transaction: invalid value");
                    }
                }
            }
        }
        protected virtual IEntityMapper CreateDataMapper() { return new EntityMapper(); }
        protected virtual void ConfigureParameters(in DbCommand command)
        {
            Func<string, object, DbParameter> AddWithValueDelegate;

            if (command is SqlCommand ms)
            {
                AddWithValueDelegate = ms.Parameters.AddWithValue;
            }
            else if (command is NpgsqlCommand pg)
            {
                AddWithValueDelegate = pg.Parameters.AddWithValue;
            }
            else if (command is SqliteCommand sql)
            {
                AddWithValueDelegate = sql.Parameters.AddWithValue;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database: {command}");
            }

            command.Parameters.Clear();

            foreach (ColumnExpression option in Statement.Options)
            {
                if (!StreamFactory.TryEvaluate(Scope, option.Expression, out object value))
                {
                    throw new InvalidOperationException();
                }

                value ??= DBNull.Value;
                string name = option.Alias;

                _ = AddWithValueDelegate(name, value);
            }
        }
        private void ConfigureDataMapper()
        {
            if (DataSchema is null) { return; }

            DataMapper = CreateDataMapper();

            foreach (PropertyDefinition property in DataSchema.Properties)
            {
                UnionType type = new();

                if (property.Type.Identifier == "boolean")
                {
                    type.IsBoolean = true;
                }
                else if (property.Type.Identifier == "number")
                {
                    type.IsNumeric = true;
                }
                else if (property.Type.Identifier == "decimal")
                {
                    type.IsNumeric = true;
                }
                else if (property.Type.Identifier == "integer")
                {
                    type.IsInteger = true;
                }
                else if (property.Type.Identifier == "datetime")
                {
                    type.IsDateTime = true;
                }
                else if (property.Type.Identifier == "string")
                {
                    type.IsString = true;
                }
                else if (property.Type.Identifier == "binary")
                {
                    type.IsBinary = true;
                }
                else if (property.Type.Identifier == "uuid")
                {
                    type.IsUuid = true;
                }
                else
                {
                    throw new InvalidOperationException();
                }

                DataMapper.Add(new PropertyMapper() { Name = property.Name, DataType = type });
            }
        }
        public void Process()
        {
            if (_stream)
            {
                try { ExecuteRequest(); }
                catch { throw; }
                finally { Dispose(); }
            }
            else
            {
                _table.Clear();
                ExecuteRequest();
                SetOutputValue();
                _next?.Process();
            }
        }
        private void SetOutputValue()
        {
            if (Statement.Response is not null)
            {
                if (_type == TokenType.Array)
                {
                    _ = Scope.TrySetValue(_output, _table);
                }
                else if (_type == TokenType.Object && _table.Count > 0)
                {
                    _ = Scope.TrySetValue(_output, _table[0]); _table.Clear();
                }
                else if (!string.IsNullOrEmpty(_output))
                {
                    _ = Scope.TrySetValue(_output, null);
                }
            }
        }
        private void ExecuteRequest()
        {
            DbTransaction transaction = null;

            using (DbConnection connection = _factory.Create(in _uri))
            {
                connection.Open();

                if (_transaction != IsolationLevel.Unspecified)
                {
                    transaction = connection.BeginTransaction(_transaction);
                }

                using (DbCommand command = connection.CreateCommand())
                {
                    if (_transaction != IsolationLevel.Unspecified)
                    {
                        command.Connection = connection;
                        command.Transaction = transaction;
                    }
                    command.CommandType = CommandType.Text;
                    command.CommandText = _script;
                    command.CommandTimeout = _timeout;

                    ConfigureParameters(in command);

                    try
                    {
                        if (Statement.Response is null)
                        {
                            int rows_affected = command.ExecuteNonQuery();
                        }
                        else
                        {
                            using (DbDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ProcessDataRecord(in reader);
                                }
                                reader.Close();
                            }
                        }

                        transaction?.Commit();
                    }
                    catch (Exception error)
                    {
                        try
                        {
                            transaction?.Rollback(); throw;
                        }
                        catch // Ignore rollback error
                        {
                            throw error;
                        }
                    }
                    finally
                    {
                        if (_stream) // clear streaming buffer
                        {
                            _ = Scope.TrySetValue(_output, null);
                        }
                    }
                }
            }
        }
        private void ProcessDataRecord(in DbDataReader reader)
        {
            if (_stream)
            {
                _record ??= new DataObject(DataMapper.Properties.Count);
            }
            else
            {
                _record = new DataObject(DataMapper.Properties.Count);
            }

            DataMapper.Map(reader, in _record);

            if (_stream)
            {
                _next?.Process();
            }
            else
            {
                _table.Add(_record);
            }
        }
    }
}