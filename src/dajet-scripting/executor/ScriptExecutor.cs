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
        private readonly MetadataCache _cache;
        public ScriptExecutor(MetadataCache cache)
        {
            _cache = cache;
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

            if (!binder.TryBind(in scope, in _cache, out error))
            {
                throw new Exception(error);
            }

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(model, out error))
            {
                throw new Exception(error);
            }

            ISqlGenerator generator;

            if (_cache.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = _cache.InfoBase.YearOffset };
            }
            else if (_cache.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = _cache.InfoBase.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {_cache.DatabaseProvider}");
            }
            
            if (!generator.TryGenerate(in model, out GeneratorResult result))
            {
                throw new Exception(result.Error);
            }

            return result;
        }
        private void ConfigureParameters(in ScriptModel model)
        {
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is DeclareStatement declare)
                {
                    if (declare.Initializer is not ScalarExpression scalar)
                    {
                        continue;
                    }

                    object value = null!;
                    string name = declare.Name.Substring(1); // remove leading @ or &
                    string literal = scalar.Literal;

                    if (!Parameters.TryGetValue(name, out _))
                    {
                        if (ScriptHelper.IsDataType(declare.Type, out Type type))
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
                                if (literal.Contains("."))
                                {
                                    value = decimal.Parse(literal, CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    value = int.Parse(literal);
                                }
                            }
                            else if (type == typeof(DateTime))
                            {
                                value = DateTime.Parse(literal.TrimStart('\"').TrimEnd('\"')).AddYears(_cache.InfoBase.YearOffset);
                            }
                            else if (type == typeof(string))
                            {
                                value = literal.TrimStart('\"').TrimEnd('\"');
                            }
                            else if (type == typeof(Guid))
                            {
                                value = new Guid(literal.TrimStart('\"').TrimEnd('\"'));
                            }
                            else if (type == typeof(byte[]))
                            {
                                value = DbUtilities.StringToByteArray(literal.Substring(2)); // remove leading 0x
                            }
                            else if (type == typeof(Entity))
                            {
                                value = Entity.Parse(scalar.Literal); // {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e}
                            }
                        }
                        else
                        {
                            // Metadata object reference parameter:
                            // Case 1. DECLARE @product Справочник.Номенклатура = {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e};
                            // Case 2. DECLARE @product Справочник.Номенклатура = "9a1984dc-3084-11ed-9cd7-408d5c93cc8e";
                            
                            MetadataObject table = _cache.GetMetadataObject(declare.Type);

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
                                    value = new Entity(entity.TypeCode, new Guid(literal.TrimStart('\"').TrimEnd('\"')));
                                }
                            }
                        }

                        Parameters.Add(name, value);
                    }
                }
            }

            foreach (var parameter in Parameters)
            {
                if (parameter.Value is Guid uuid)
                {
                    Parameters[parameter.Key] = SQLHelper.GetSqlUuid(uuid);
                }
                else if (parameter.Value is bool boolean)
                {
                    if (_cache.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        Parameters[parameter.Key] = new byte[] { Convert.ToByte(boolean) };
                    }
                }
                else if (parameter.Value is Entity entity)
                {
                    Parameters[parameter.Key] = SQLHelper.GetSqlUuid(entity.Identity);
                }

                if (DeclareStatementExists(in model, parameter.Key))
                {
                    continue;
                }

                if (parameter.Value == null)
                {
                    continue; // TODO TokenType.NULL
                }

                Type parameterType = parameter.Value.GetType();

                DeclareStatement declare = new()
                {
                    Name = "@" + parameter.Key,
                    Type = ScriptHelper.GetDataTypeLiteral(parameterType),
                    Initializer = new ScalarExpression()
                    {
                        Token = ScriptHelper.GetDataTypeToken(parameterType),
                        Literal = parameter.Value.ToString()!
                    }
                };

                model.Statements.Insert(0, declare);
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

            IQueryExecutor executor = _cache.CreateQueryExecutor();

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

            IQueryExecutor executor = _cache.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(result.Script, 10, Parameters))
            {
                yield return result.Mapper.Map<TEntity>(in reader);
            }
        }
    }
}