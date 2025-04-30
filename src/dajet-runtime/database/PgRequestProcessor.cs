using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Npgsql;
using System.Data;
using PropertyDefinition = DaJet.Scripting.Model.PropertyDefinition;
using TypeDefinition = DaJet.Scripting.Model.TypeDefinition;

namespace DaJet.Runtime
{
    public sealed class PgRequestProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private Uri _uri;
        private readonly RequestStatement _statement;
        private readonly IDbConnectionFactory _factory;
        private int _timeout = 30; // seconds
        private string _script = string.Empty;
        private string _output = string.Empty;
        private TypeDefinition _schema = null;
        private TokenType _type = TokenType.Ignore;
        private EntityMapper _mapper = new();
        private List<DataObject> _table = new();
        public PgRequestProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not RequestStatement statement)
            {
                throw new ArgumentException(nameof(RequestStatement));
            }

            _statement = statement;

            ConfigureOutputValue();
            ConfigureOutputDataMapper();

            _uri = _scope.GetUri(_statement.Target);
            _factory = DbConnectionFactory.GetFactory(in _uri);

            ConfigureProcessorOptions();
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        private void ConfigureOutputValue()
        {
            if (_statement.Response is not null)
            {
                _output = _statement.Response.Identifier;

                if (!_scope.TryGetDeclaration(_output, out _, out DeclareStatement declare))
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

                if (!_scope.TryGetDefinition(declare.TypeOf.Identifier, out _schema))
                {
                    throw new InvalidOperationException();
                }
            }
        }
        private void ConfigureOutputDataMapper()
        {
            if (_schema is null) { return; }

            foreach (PropertyDefinition property in _schema.Properties)
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

                _mapper.AddPropertyMapper(property.Name, in type);
            }
        }
        private void ConfigureProcessorOptions()
        {
            Dictionary<string, string> headers = new(_statement.Headers.Count);

            foreach (ColumnExpression option in _statement.Headers)
            {
                if (!StreamFactory.TryEvaluate(in _scope, option.Expression, out object value))
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
            }
        }
        public void Process()
        {
            _table.Clear();

            ExecuteRequest();

            ConfigureOutput();

            _next?.Process();
        }
        private void ProcessRecord(in NpgsqlDataReader reader)
        {
            DataObject record = new(_mapper.Properties.Count);

            _mapper.Map(reader, in record);

            _table.Add(record);
        }
        private void ConfigureOutput()
        {
            if (_statement.Response is not null)
            {
                if (_type == TokenType.Array)
                {
                    _ = _scope.TrySetValue(_output, _table);
                }
                else if (_type == TokenType.Object && _table.Count > 0)
                {
                    _ = _scope.TrySetValue(_output, _table[0]); _table.Clear();
                }
                else if (!string.IsNullOrEmpty(_output))
                {
                    _ = _scope.TrySetValue(_output, null);
                }
            }
        }
        private void ConfigureQueryParameters(in NpgsqlCommand command)
        {
            command.Parameters.Clear();

            foreach (ColumnExpression option in _statement.Options)
            {
                if (!StreamFactory.TryEvaluate(in _scope, option.Expression, out object value))
                {
                    throw new InvalidOperationException();
                }

                value ??= DBNull.Value;
                string name = option.Alias;

                command.Parameters.AddWithValue(name, value);
            }
        }
        private void ExecuteRequest()
        {
            using (NpgsqlConnection connection = _factory.Create(in _uri) as NpgsqlConnection)
            {
                connection.Open();

                NpgsqlTransaction transaction = connection.BeginTransaction();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.Connection = connection;
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = _script;
                    command.CommandTimeout = _timeout;

                    ConfigureQueryParameters(in command);

                    try
                    {
                        if (_statement.Response is null)
                        {
                            int rows_affected = command.ExecuteNonQuery();
                        }
                        else
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ProcessRecord(in reader);
                                }
                                reader.Close();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception error)
                    {
                        try
                        {
                            transaction.Rollback(); throw;
                        }
                        catch
                        {
                            throw error;
                        }
                    }
                    finally
                    {
                        // clear streaming buffer
                    }
                }
            }
        }
    }
}