using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Data;
using System.Globalization;

namespace DaJet.Scripting
{
    public sealed class ScriptExecutor
    {
        private readonly IMetadataProvider _metadata;
        public ScriptExecutor(IMetadataProvider provider)
        {
            _metadata = provider;
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

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in model, out ScriptScope scope, out error))
            {
                throw new Exception(error);
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in _metadata, out error))
            {
                throw new Exception(error);
            }

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(model, out error))
            {
                throw new Exception(error);
            }

            ISqlGenerator generator;

            if (_metadata.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = _metadata.YearOffset };
            }
            else if (_metadata.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = _metadata.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {_metadata.DatabaseProvider}");
            }
            
            if (!generator.TryGenerate(in model, in _metadata, out GeneratorResult result))
            {
                throw new Exception(result.Error);
            }

            ConfigureParameters(in model);

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

                    if (declare.Type.Binding is TypeDefinition) { continue; }

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

                            MetadataObject table = _metadata.GetMetadataObject(declare.Type.Identifier);

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
                    if (_metadata.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        Parameters[parameter.Key] = new byte[] { Convert.ToByte(boolean) };
                    }
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    Parameters[parameter.Key] = dateTime.AddYears(_metadata.YearOffset);
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
        public IEnumerable<Dictionary<string, object>> ExecuteReader(string script)
        {
            GeneratorResult result = PrepareScript(in script);

            if (!result.Success)
            {
                throw new Exception(result.Error);
            }

            IQueryExecutor executor = _metadata.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(result.Script, 10, Parameters))
            {
                yield return result.Mapper.Map(in reader);
            }
        }
        public IEnumerable<TEntity> ExecuteReader<TEntity>(string script) where TEntity : class, new()
        {
            GeneratorResult result = PrepareScript(in script);

            if (!result.Success)
            {
                throw new Exception(result.Error);
            }

            IQueryExecutor executor = _metadata.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(result.Script, 10, Parameters))
            {
                yield return result.Mapper.Map<TEntity>(in reader);
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

            TypeDefinition definition = new()
            {
                Name = statement.Identifier
            };

            foreach (ColumnDefinition column in statement.Columns)
            {
                //binder.TryBind(column.Type, in _metadata);

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

            //TODO: _metadata.GetDbConfigurator().CreateTableOfType(statement.Type, statement.Name);
        }
        #endregion
    }
}