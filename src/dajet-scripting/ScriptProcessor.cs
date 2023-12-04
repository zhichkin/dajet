using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Globalization;
using System.Text;

namespace DaJet.Scripting
{
    public static class ScriptProcessor
    {
        public static bool TryTranspile(in IMetadataProvider context, in string script, out ScriptDetails result, out string error)
        {
            result = null;

            if (!TryTranspile(in context, in script, out ScriptModel model, out List<ScriptStatement> statements, out error))
            {
                return false;
            }

            try
            {
                ConfigureParameters(in context, in model, out Dictionary<string, object> parameters);

                result = new ScriptDetails()
                {
                    Mappers = GetEntityMappers(in statements),
                    SqlScript = AssembleSqlScript(in statements),
                    Parameters = parameters
                };
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return result is not null;
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
        public static bool TryTranspile(in IMetadataProvider context, in string script, out ScriptModel model, out List<ScriptStatement> statements, out string error)
        {
            statements = null;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    return false;
                }
            }

            ThrowImportStatementAreNotSupported(in model);

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in model, out ScriptScope scope, out error))
            {
                return false;
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in context, out error))
            {
                return false;
            }

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(model, out error))
            {
                 return false;
            }

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
        public static void ConfigureParameters(in IMetadataProvider context, in ScriptModel model, out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();

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

                    if (!parameters.TryGetValue(name, out _))
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
                                value = Entity.Parse(scalar.Literal); // {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e}
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
            }

            // remove unnecessary parameters provided by caller
            List<string> keys_to_remove = new();
            foreach (var key in parameters.Keys)
            {
                if (!DeclareStatementExists(in model, key))
                {
                    keys_to_remove.Add(key);
                }
            }

            foreach (string key in keys_to_remove)
            {
                parameters.Remove(key);
            }

            // format parameter values
            foreach (var parameter in parameters)
            {
                if (parameter.Value is Guid uuid)
                {
                    parameters[parameter.Key] = uuid.ToByteArray();
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
                else if (parameter.Value is Entity entity)
                {
                    parameters[parameter.Key] = entity.Identity.ToByteArray();
                }
                else if (parameter.Value is List<Dictionary<string, object>> table)
                {
                    parameters[parameter.Key] = new TableValuedParameter()
                    {
                        Name = parameter.Key,
                        Value = table,
                        DbName = GetTypeIdentifier(in model, parameter.Key)
                    };
                }
            }
        }
        private static string GetTypeIdentifier(in ScriptModel script, in string name)
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
        private static bool DeclareStatementExists(in ScriptModel model, string name)
        {
            foreach (SyntaxNode statement in model.Statements)
            {
                if (statement is not DeclareStatement declare)
                {
                    continue;
                }

                if (declare.Name[1..] == name) // remove leading @ or &
                {
                    return true;
                }
            }
            return false;
        }
        public static string AssembleSqlScript(in List<ScriptStatement> statements)
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
        public static List<EntityMapper> GetEntityMappers(in List<ScriptStatement> statements)
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
    }
}