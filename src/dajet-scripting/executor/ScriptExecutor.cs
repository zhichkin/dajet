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

            ConfigureParameters(in model);

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
            
            if (!generator.TryGenerate(in model, out GeneratorResult result))
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
                    if (declare.Initializer is not ScalarExpression scalar)
                    {
                        // Set default value if parameter value is not initialized in script.
                        scalar = ScriptHelper.CreateDefaultScalar(in declare);
                        declare.Initializer = scalar;
                    }

                    object value = null!;
                    string name = declare.Name[1..]; // remove leading @
                    string literal = scalar.Literal;

                    // Synchronize parameters defined by script and provided by caller.
                    // Parameters defined by script must present anyway!
                    // Parameter values provided by the caller overrides parameter values set by script.
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
            }
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

            ProcessCreateStatements(in model);
        }
        private void ProcessCreateStatements(in ScriptModel script)
        {
            IDbConfigurator configurator = _metadata.GetDbConfigurator();

            configurator.CreateDatabase();
        }
        private bool IsRegularDatabase
        {
            get
            {
                IQueryExecutor executor = _metadata.CreateQueryExecutor();
                string script = SQLHelper.GetTableExistsScript("_yearoffset");
                return !(executor.ExecuteScalar<int>(in script, 10) == 1);
            }
        }
        private bool TableExists(in string name)
        {
            IQueryExecutor executor = _metadata.CreateQueryExecutor();
            string script = SQLHelper.GetTableExistsScript(name);
            return (executor.ExecuteScalar<int>(in script, 10) == 1);
        }
        
        private UnionType ResolveDataType(TypeIdentifier type, out List<TypeDef> references)
        {
            UnionType union = new();

            references = new List<TypeDef>();

            if (!union.ApplySystemType(type.Identifier, out UnionTag _))
            {
                TypeDef target = ApplyEntityType(type.Identifier, in union);
                
                references.Add(target);
            }

            return union;
        }
        private TypeDef ApplyEntityType(in string identifier, in UnionType union)
        {
            TypeDef type = _metadata.GetTypeDefinition(in identifier) ?? throw new InvalidOperationException($"Undefined type: [{identifier}]");

            if (!type.IsEntity)
            {
                throw new InvalidOperationException($"Type [{identifier}] is not ENTITY");
            }

            union.TypeCode = type.Code;

            return type;
        }
        private TypeDef GetSystemTypeDef(in CreateTypeStatement statement)
        {
            //if (TableExists("md-types")) { return; }

            TypeDef metadata = _metadata.GetTypeDefinition("Metadata");
            int ordinal = metadata.Properties.Count;

            TypeDef definition = new()
            {
                Ref = new Entity(3, Guid.NewGuid()),
                Name = statement.Name,
                TableName = "md-types",
                BaseType = metadata.Ref
            };

            foreach (ColumnDefinition column in statement.Columns)
            {
                PropertyDef property = new()
                {
                    Ref = new Entity(4, Guid.NewGuid()),
                    Name = column.Name,
                    Owner = definition.Ref,
                    Ordinal = ++ordinal,
                    ColumnName = column.Name,
                    DataType = ResolveDataType(column.Type, out List<TypeDef> references),
                    Qualifier1 = column.Type.Qualifier1,
                    Qualifier2 = column.Type.Qualifier2,
                    IsVersion = column.IsVersion,
                    IsNullable = column.IsNullable,
                    IsPrimaryKey = statement.PrimaryKey.Contains(column.Name),
                    IsIdentity = column.IsIdentity,
                    IdentitySeed = column.IdentitySeed,
                    IdentityIncrement = column.IdentityIncrement
                };
                definition.Properties.Add(property);

                UnionType type = property.DataType;

                if (type.IsEntity)
                {
                    List<RelationDef> relations = new();

                    foreach (TypeDef target in references)
                    {
                        relations.Add(new RelationDef()
                        {
                            Source = property.Ref,
                            Target = target.Ref
                        });
                    }
                }
            }

            //IQueryExecutor executor = _metadata.CreateQueryExecutor();

            return definition;
        }
    }
}