using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Data;

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

            if (Parameters.Count > 0)
            {
                ConfigureParameters(in model);
            }

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
                generator = new MsSqlGenerator();
            }
            else if (_cache.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator();
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
            foreach (var parameter in Parameters)
            {
                string parameterName = parameter.Key.StartsWith('@') ? parameter.Key : "@" + parameter.Key;

                if (parameter.Value is Guid uuid)
                {
                    Parameters[parameter.Key] = SQLHelper.GetSqlUuid(uuid);
                }
                else if (parameter.Value is EntityRef entity)
                {
                    Parameters[parameter.Key] = SQLHelper.GetSqlUuid(entity.Identity);
                }

                if (DeclareStatementExists(in model, parameterName))
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
                    Name = parameterName,
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

                if (declare.Name == name)
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