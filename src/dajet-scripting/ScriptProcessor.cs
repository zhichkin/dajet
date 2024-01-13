using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Data;
using System.Globalization;
using System.Text;

namespace DaJet.Scripting
{
    public static class ScriptProcessor
    {
        public static bool TryTranspile(in IMetadataProvider context,
            in string script, in Dictionary<string, object> parameters,
            out ScriptDetails result, out string error)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentNullException(nameof(script));
            }

            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            result = new ScriptDetails();

            foreach (var item in parameters)
            {
                result.Parameters.Add(item.Key, item.Value);
            }

            if (!TryTranspile(in context, in script, result.Parameters, out List<ScriptStatement> statements, out error))
            {
                result = null; return false;
            }

            try
            {
                result.Mappers = GetEntityMappers(in statements);
                result.SqlScript = AssembleSqlScript(in statements);
            }
            catch (Exception exception)
            {
                result = null;
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (result is not null);
        }
        private static void ThrowImportStatementAreNotSupported(in ScriptModel model)
        {
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is ImportStatement import)
                {
                    throw new NotSupportedException(import.GetType().ToString());
                }
            }
        }
        private static bool TryTranspile(in IMetadataProvider context,
            in string script, in Dictionary<string, object> parameters,
            out List<ScriptStatement> statements, out string error)
        {
            statements = null;

            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    return false;
                }
            }

            ThrowImportStatementAreNotSupported(in model);

            OverrideEntityParameters(in model, in parameters);

            if (!new MetadataBinder().TryBind(model, in context, out _, out List<string> errors))
            {
                StringBuilder message = new();

                for (int i = 0; i < errors.Count; i++)
                {
                    if (i > 0) { message.AppendLine(); }

                    message.Append(errors[i]);
                }

                error = message.ToString();

                if (string.IsNullOrEmpty(error))
                {
                    error = "Unknown binding error";
                }

                return false;
            }
            
            if (!new ScriptTransformer().TryTransform(model, out error))
            {
                 return false;
            }

            ConfigureParameters(in context, in model, in parameters);

            ConfigureEntityParameters(in model, in context, in parameters);

            ISqlGenerator generator;

            if (context.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = context.YearOffset };
            }
            else if (context.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = context.YearOffset };
            }
            else
            {
                error = $"Unsupported database provider: {context.DatabaseProvider}";
                return false;
            }

            if (!generator.TryGenerate(in model, in context, out statements, out error))
            {
                return false;
            }

            return true;
        }
        private static string AssembleSqlScript(in List<ScriptStatement> statements)
        {
            if (statements is null)
            {
                return string.Empty;
            }

            StringBuilder script = new();

            for (int i = 0; i < statements.Count; i++)
            {
                ScriptStatement statement = statements[i];

                if (string.IsNullOrEmpty(statement.Script))
                {
                    continue; //NOTE: declaration of parameters
                }

                script.AppendLine(statement.Script);
            }

            return script.ToString();
        }
        private static List<EntityMapper> GetEntityMappers(in List<ScriptStatement> statements)
        {
            List<EntityMapper> mappers = new();

            if (statements is null)
            {
                return mappers;
            }

            foreach (ScriptStatement command in statements)
            {
                if (command.Mapper.Properties.Count > 0)
                {
                    mappers.Add(command.Mapper);
                }
            }

            return mappers;
        }
        private static DeclareStatement GetDeclareStatementByName(in ScriptModel model, in string name)
        {
            string fullName = "@" + name;

            foreach (SyntaxNode node in model.Statements)
            {
                if (node is DeclareStatement declare && declare.Name == fullName)
                {
                    return declare;
                }
            }
            return null;
        }
        private static void OverrideEntityParameters(in ScriptModel model, in Dictionary<string, object> parameters)
        {
            foreach (var item in parameters)
            {
                if (item.Value is not Entity entity) { continue; }
                
                DeclareStatement declare = GetDeclareStatementByName(in model, item.Key);

                if (declare is null) { continue; }

                // any entity type value specifies "entity" literal

                declare.Type.Identifier = ScriptHelper.GetDataTypeLiteral(typeof(Entity));

                // entity type code provided by user determines the data type for parameter

                ScalarExpression scalar = new()
                {
                    Token = TokenType.Entity,
                    Literal = entity.ToString()
                };

                declare.Initializer = scalar; 
            }
        }
        private static void ConfigureParameters(in IMetadataProvider context, in ScriptModel model, in Dictionary<string, object> parameters)
        {
            // add script parameters to the dictionary if they are missing
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is not DeclareStatement declare) { continue; }

                if (declare.Initializer is SelectExpression) { continue; }
                
                // 0. Database UDT parameter must be provided by the caller !!!
                
                if (declare.Type.Binding is EntityDefinition) { continue; }

                // 1. Set default value if parameter value is not initialized in script.

                if (declare.Initializer is not ScalarExpression scalar)
                {
                    scalar = ScriptHelper.CreateDefaultScalar(in declare);
                    declare.Initializer = scalar;
                }

                // 2. Synchronize parameters defined by script and provided by the caller.
                // -  Parameters defined by script must present anyway!
                // -  Parameter values provided by the caller overrides parameter values defined by script.

                object value = null;
                string name = declare.Name[1..]; // remove leading @
                string literal = scalar.Literal;

                if (!parameters.ContainsKey(name))
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
                            value = DbUtilities.StringToByteArray(literal[2..]); // remove leading 0x
                        }
                        else if (type == typeof(Entity))
                        {
                            // Metadata object reference parameter:
                            // DECLARE @product entity = {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e};
                            value = Entity.Parse(scalar.Literal);
                        }
                    }
                    else
                    {
                        // Metadata object reference parameter:
                        // DECLARE @product Справочник.Номенклатура = "9a1984dc-3084-11ed-9cd7-408d5c93cc8e";

                        MetadataObject table = context.GetMetadataObject(declare.Type.Identifier);

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
                    parameters.Add(name, value);
                }
            }

            // remove unnecessary parameters provided by caller
            List<string> keys_to_remove = new();
            foreach (var key in parameters.Keys)
            {
                if (GetDeclareStatementByName(in model, key) is null)
                {
                    keys_to_remove.Add(key);
                }
            }
            foreach (string key in keys_to_remove)
            {
                parameters.Remove(key);
            }

            // format parameter values for the .NET data provider
            foreach (var parameter in parameters)
            {
                if (parameter.Value is Entity entity)
                {
                    parameters[parameter.Key] = entity.Identity.ToByteArray();
                }
                else if (parameter.Value is bool boolean)
                {
                    if (context.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        parameters[parameter.Key] = new byte[] { Convert.ToByte(boolean) };
                    }
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    parameters[parameter.Key] = dateTime.AddYears(context.YearOffset);
                }
                else if (parameter.Value is Guid uuid)
                {
                    parameters[parameter.Key] = uuid.ToByteArray();
                }
                else if (parameter.Value is List<Dictionary<string, object>> table)
                {
                    DeclareStatement declare = GetDeclareStatementByName(in model, parameter.Key);

                    parameters[parameter.Key] = new TableValuedParameter()
                    {
                        Name = parameter.Key,
                        Value = table,
                        DbName = declare is null ? string.Empty : declare.Type.Identifier
                    };
                }
            }
        }

        private static void ConfigureEntityParameters(in ScriptModel model, in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is not DeclareStatement declare) { continue; }

                if (declare.Initializer is SelectExpression select)
                {
                    ScriptStatement statement = CreateScriptStatement(in context, in select);

                    List<string> variables = new VariablesExtractor().GetVariables(select);

                    Dictionary<string, object> select_parameters = new();

                    foreach (string name in variables)
                    {
                        if (parameters.TryGetValue(name, out object value))
                        {
                            select_parameters.Add(name, value);
                        }
                    }

                    Entity entity = SelectEntityValue(in context, in statement, in select_parameters);

                    if (entity.IsUndefined)
                    {
                        if (declare.Type.Binding is Entity value)
                        {
                            entity = value;
                        }
                    }

                    declare.Initializer = new ScalarExpression()
                    {
                        Token = TokenType.Entity,
                        Literal = entity.ToString()
                    };

                    parameters.Add(declare.Name[1..], entity.Identity.ToByteArray());
                }
            }
        }
        private static ScriptStatement CreateScriptStatement(in IMetadataProvider context, in SelectExpression select)
        {
            ScriptModel model = new();

            model.Statements.Add(new SelectStatement()
            {
                Expression = select
            });

            ISqlGenerator generator;

            if (context.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = context.YearOffset };
            }
            else if (context.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = context.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {context.DatabaseProvider}");
            }

            if (!generator.TryGenerate(in model, in context, out List<ScriptStatement> statements, out string error))
            {
                throw new Exception(error);
            }

            if (statements is not null && statements.Count > 0)
            {
                return statements[0];
            }

            throw new InvalidOperationException("Entity parameters configuration error");
        }
        private static Entity SelectEntityValue(in IMetadataProvider context, in ScriptStatement statement, in Dictionary<string, object> parameters)
        {
            object value = null;

            IQueryExecutor executor = context.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                value = statement.Mapper.Properties[0].GetValue(in reader); break;
            }

            if (value is Entity entity)
            {
                return entity;
            }

            return Entity.Undefined;
        }
    }
}