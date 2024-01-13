using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet
{
    public static class ScriptEngine
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";

        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataObjectJsonConverter _converter = new();
        private static void WriteResultToFile(in DataObject input)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, input, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    Console.WriteLine(json);

                    FileLogger.Default.Write(json);
                }
            }
        }
        public static void Execute(in string filePath)
        {
            string script;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            IMetadataProvider context = new OneDbMetadataProvider(MS_CONNECTION);
            //IMetadataProvider context = new OneDbMetadataProvider(PG_CONNECTION);

            using (OneDbConnection connection = new(context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;
                    command.Parameters.Add("КодВалюты", "840");
                    //command.Parameters.Add("Валюта", new Entity(60, Guid.Empty));
                    //command.Parameters.Add("Номенклатура", new Entity(50, Guid.Empty));

                    foreach (DataObject record in command.StreamReader())
                    {
                        //for (int i = 0; i < record.Count(); i++)
                        //{
                        //    string name = record.GetName(i);
                        //    object value = record.GetValue(i);
                        //}

                        WriteResultToFile(in record);
                    }
                }
            }
        }
        #region "NEW SCRIPT PROCESSOR ENGINE"
        private static void TODO_ExecuteScript(in string script, in IMetadataProvider context)
        {
            Dictionary<string, object> parameters = new();

            if (!new ScriptParser().TryParse(in script, out ScriptModel model, out string error))
            {
                Console.WriteLine(error);
            }

            OverrideEntityParameters(in model, in parameters);

            if (!new MetadataBinder().TryBind(model, in context, out _, out List<string> errors))
            {
                foreach (string text in errors)
                {
                    Console.WriteLine(text);
                }
            }

            if (!new ScriptTransformer().TryTransform(model, out error))
            {
                Console.WriteLine(error);
            }

            ConfigureParameters(in context, in model, in parameters);

            ///TODO: ExecuteImportStatements(in model); <see cref="DaJet.Scripting.ScriptExecutor"/>

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
                return;
            }

            if (!generator.TryGenerate(in model, in context, out List<ScriptStatement> statements, out error))
            {
                return;
            }

            ScriptDetails details = new()
            {
                Parameters = parameters
            };

            try
            {
                details.Mappers = GetEntityMappers(in statements);
                details.SqlScript = AssembleSqlScript(in statements);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }
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
        #endregion
    }
}