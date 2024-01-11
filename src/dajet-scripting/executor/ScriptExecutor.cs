using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Model;
using DaJet.Scripting.Model;
using System.Data;
using System.Globalization;

namespace DaJet.Scripting
{
    public sealed class ScriptExecutor
    {
        private readonly IDataSource _source;
        private readonly IMetadataService _metadata;
        private readonly IMetadataProvider _context; // execution context database
        public ScriptExecutor(IMetadataProvider context, IMetadataService metadata, IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
        public Dictionary<string, object> Parameters { get; } = new();
        public GeneratorResult PrepareScript(in string script)
        {
            string error;
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    throw new Exception(error);
                }
            }

            if (!new SchemaBinder().TryBind(model, in _context, out _, out List<string> errors))
            {
                if (errors is not null && errors.Count > 0)
                {
                    throw new Exception(errors[0]);
                }
                else
                {
                    throw new Exception("Unknown binding error");
                }
            }

            //TODO: remove deprecated code
            //if (!new ScopeBuilder().TryBuild(in model, out ScriptScope scope, out error))
            //{
            //    throw new Exception(error);
            //}
            //if (!new MetadataBinder().TryBind(in scope, in _context, out error))
            //{
            //    throw new Exception(error);
            //}

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(model, out error))
            {
                throw new Exception(error);
            }

            ConfigureParameters(in model);

            ExecuteImportStatements(in model);

            // execute main context script

            ISqlGenerator generator;

            if (_context.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = _context.YearOffset };
            }
            else if (_context.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = _context.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {_context.DatabaseProvider}");
            }
            
            if (!generator.TryGenerate(in model, in _context, out GeneratorResult result))
            {
                throw new Exception(result.Error);
            }

            return result;
        }
        private void ConfigureParameters(in ScriptModel model)
        {
            // configure parameters for database provider
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is DeclareStatement declare)
                {
                    // 0. UDT parameter must (!) be provided by caller - it has no default value anyway

                    if (declare.Type.Binding is EntityDefinition) { continue; }

                    // 1. Set default value if parameter value is not initialized in script.

                    if (declare.Initializer is not ScalarExpression scalar)
                    {
                        scalar = ScriptHelper.CreateDefaultScalar(in declare);
                        declare.Initializer = scalar;
                    }

                    object value = null!;
                    string name = declare.Name[1..]; // remove leading @
                    string literal = scalar.Literal;

                    // 2. Synchronize parameters defined by script and provided by caller.
                    // -  Parameters defined by script must present anyway!
                    // -  Parameter values provided by the caller overrides parameter values set by script.

                    if (!Parameters.TryGetValue(name, out _))
                    {
                        if (ScriptHelper.IsDataType(declare.Type.Identifier, out Type type))
                        {
                            if (type == typeof(bool))
                            {
                                if (literal.ToLowerInvariant() == "true")
                                {
                                    value = true;
                                }
                                else if (literal.ToLowerInvariant() == "false")
                                {
                                    value = false;
                                }
                            }
                            else if (type == typeof(decimal))
                            {
                                if (literal.Contains('.'))
                                {
                                    value = decimal.Parse(literal, CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    value = int.Parse(literal, CultureInfo.InvariantCulture);
                                }
                            }
                            else if (type == typeof(DateTime))
                            {
                                value = DateTime.Parse(literal);
                            }
                            else if (type == typeof(string))
                            {
                                value = literal;
                            }
                            else if (type == typeof(Guid))
                            {
                                value = new Guid(literal);
                            }
                            else if (type == typeof(byte[]))
                            {
                                value = DbUtilities.StringToByteArray(literal.Substring(2)); // remove leading 0x
                            }
                            else if (type == typeof(Entity))
                            {
                                // Metadata object reference parameter:
                                // DECLARE @product entity = {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e};
                                value = Entity.Parse(scalar.Literal); // {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e}
                            }
                        }
                        else
                        {
                            // Metadata object reference parameter:
                            // DECLARE @product Справочник.Номенклатура = "9a1984dc-3084-11ed-9cd7-408d5c93cc8e";

                            MetadataObject table = _context.GetMetadataObject(declare.Type.Identifier);

                            if (table is ApplicationObject entity)
                            {
                                if (Entity.TryParse(literal, out Entity initializer))
                                {
                                    if (initializer.TypeCode == entity.TypeCode)
                                    {
                                        value = initializer;
                                    }
                                }
                                else
                                {
                                    value = new Entity(entity.TypeCode, new Guid(literal));
                                }
                            }
                        }

                        Parameters.Add(name, value);
                    }
                }
            }

            // remove unnecessary parameters provided by caller
            List<string> keys_to_remove = new();
            foreach (var key in Parameters.Keys)
            {
                if (!DeclareStatementExists(in model, key))
                {
                    keys_to_remove.Add(key);
                }
            }

            foreach (string key in keys_to_remove)
            {
                Parameters.Remove(key);
            }

            // format parameter values
            foreach (var parameter in Parameters)
            {
                if (parameter.Value is Guid uuid)
                {
                    Parameters[parameter.Key] = uuid.ToByteArray();
                }
                else if (parameter.Value is bool boolean)
                {
                    if (_context.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        Parameters[parameter.Key] = new byte[] { Convert.ToByte(boolean) };
                    }
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    Parameters[parameter.Key] = dateTime.AddYears(_context.YearOffset);
                }
                else if (parameter.Value is Entity entity)
                {
                    Parameters[parameter.Key] = entity.Identity.ToByteArray();
                }
                else if (parameter.Value is List<Dictionary<string, object>> table)
                {
                    Parameters[parameter.Key] = new TableValuedParameter()
                    {
                        Name = parameter.Key,
                        Value = table,
                        DbName = GetTypeIdentifier(in model, parameter.Key)
                    };
                }
            }
        }
        private string GetTypeIdentifier(in ScriptModel script, in string name)
        {
            foreach (SyntaxNode node in script.Statements)
            {
                if (node is not DeclareStatement declare)
                {
                    continue;
                }

                if (declare.Name[1..] == name) // remove leading @
                {
                    return declare.Type.Identifier;
                }
            }

            return null;
        }
        private bool DeclareStatementExists(in ScriptModel model, string name)
        {
            foreach (SyntaxNode statement in model.Statements)
            {
                if (statement is not DeclareStatement declare)
                {
                    continue;
                }

                if (declare.Name.Substring(1) == name) // remove leading @ or &
                {
                    return true;
                }
            }
            return false;
        }

        private void ExecuteImportStatements(in ScriptModel model)
        {
            List<SyntaxNode> items_to_remove = new();

            foreach (SyntaxNode node in model.Statements)
            {
                if (node is ImportStatement import)
                {
                    items_to_remove.Add(node);

                    ExecuteImportStatement(in import);
                }
            }

            foreach (SyntaxNode node in items_to_remove)
            {
                model.Statements.Remove(node);
            }
        }
        private void ExecuteImportStatement(in ImportStatement import)
        {
            Uri uri = new(import.Source);

            if (uri.Scheme != "dajet")
            {
                throw new InvalidOperationException($"Unknown data source scheme: {uri.Scheme}");
            }

            string target = uri.Host;
            string script = uri.AbsolutePath;
            string scriptPath = target + "/" + script;

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(target) ?? throw new ArgumentException($"Target not found: {target}");
            ScriptRecord record = _source.Select<ScriptRecord>(scriptPath) ?? throw new ArgumentException($"Script not found: {script}");

            if (!_metadata.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ScriptExecutor executor = new(provider, _metadata, _source);

            string query = uri.Query;

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.TrimStart('?');

                string[] parameters = query.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                foreach (string parameter in parameters)
                {
                    string[] replace = parameter.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    string parameterName = replace[0];
                    string variableName = (replace.Length > 1) ? replace[1] : parameterName;
                    
                    if (Parameters.TryGetValue(variableName, out object value))
                    {
                        executor.Parameters.Add(parameterName, value);
                    }
                }
            }

            List<Dictionary<string, object>> result = new();

            foreach (var entity in executor.ExecuteReader(record.Script))
            {
                result.Add(entity);
            }

            if (import.Target is not null && import.Target.Count > 0)
            {
                VariableReference variable = import.Target[0];

                string key = variable.Identifier[1..];

                if (variable.Binding is EntityDefinition type)
                {
                    Parameters[key] = new TableValuedParameter()
                    {
                        Name = key,
                        Value = result,
                        DbName = type.Name
                    };
                }
            }
        }

        public IEnumerable<Dictionary<string, object>> ExecuteReader(string script)
        {
            GeneratorResult result = PrepareScript(in script);

            if (!result.Success) { throw new Exception(result.Error); }

            IQueryExecutor executor = _context.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(result.Script, 10, Parameters))
            {
                yield return result.Mapper.Map(in reader);
            }
        }
        public IEnumerable<TEntity> ExecuteReader<TEntity>(string script) where TEntity : class, new()
        {
            GeneratorResult result = PrepareScript(in script);

            if (!result.Success) { throw new Exception(result.Error); }

            IQueryExecutor executor = _context.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(result.Script, 10, Parameters))
            {
                yield return result.Mapper.Map<TEntity>(in reader);
            }
        }

        public List<List<Dictionary<string, object>>> Execute(string script)
        {
            List<List<Dictionary<string, object>>> batch = new();

            GeneratorResult generator = PrepareScript(in script);

            if (!generator.Success) { throw new Exception(generator.Error); }

            IQueryExecutor executor = _context.CreateQueryExecutor();

            int next = 0;
            EntityMapper mapper = null;
            List<Dictionary<string, object>> result = null;

            foreach (IDataReader reader in executor.ExecuteBatch(generator.Script, 10, Parameters))
            {
                if (reader is null) // new result
                {
                    result = new List<Dictionary<string, object>>();
                    mapper = generator.Statements[next++].Mapper;
                    batch.Add(result);
                }
                else
                {
                    result.Add(mapper.Map(in reader));
                }
            }

            return batch;
        }

        public void PrepareScript(in string script, out ScriptModel model, out List<ScriptStatement> statements)
        {
            string error;
            
            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    throw new Exception(error);
                }
            }

            if (!new SchemaBinder().TryBind(model, in _context, out _, out List<string> errors))
            {
                if (errors is not null && errors.Count > 0)
                {
                    throw new Exception(errors[0]);
                }
                else
                {
                    throw new Exception("Unknown binding error");
                }
            }

            //TODO: remove deprecated code
            //if (!new ScopeBuilder().TryBuild(in model, out ScriptScope scope, out error))
            //{
            //    throw new Exception(error);
            //}
            //if (!new MetadataBinder().TryBind(in scope, in _context, out error))
            //{
            //    throw new Exception(error);
            //}

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(model, out error))
            {
                throw new Exception(error);
            }

            ConfigureParameters(in model);

            ExecuteImportStatements(in model);

            // execute main context script

            ISqlGenerator generator;

            if (_context.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = _context.YearOffset };
            }
            else if (_context.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = _context.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {_context.DatabaseProvider}");
            }

            if (!generator.TryGenerate(in model, in _context, out statements, out error))
            {
                throw new Exception(error);
            }
        }

        #region "DaJet DDL"
        public void ExecuteNonQuery(in string script)
        {
            string error;
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    throw new Exception(error);
                }
            }

            ProcessStatements(in model);
        }
        private void ProcessStatements(in ScriptModel script)
        {
            foreach (SyntaxNode statement in script.Statements)
            {
                if (statement is CreateTypeStatement createType)
                {
                    ProcessStatement(in createType);
                }
                else if (statement is CreateTableStatement createTable)
                {
                    ProcessStatement(in createTable);
                }
            }
        }
        private void ProcessStatement(in CreateTypeStatement statement)
        {
            //MetadataBinder binder = new();

            EntityDefinition definition = new()
            {
                Name = statement.Identifier
            };

            foreach (ColumnDefinition column in statement.Columns)
            {
                //binder.TryBind(column.Type, in _context);

                MetadataProperty property = new()
                {
                    Name = column.Name
                };

                if (column.Type.Binding is Type type)
                {

                }

                property.Columns.Add(new MetadataColumn()
                {
                    Name = column.Name
                });

                definition.Properties.Add(property);
            }
        }
        private void ProcessStatement(in CreateTableStatement statement)
        {
            throw new NotImplementedException();

            //TODO: _context.GetDbConfigurator().CreateTableOfType(statement.Type, statement.Name);
        }
        #endregion
    }
}