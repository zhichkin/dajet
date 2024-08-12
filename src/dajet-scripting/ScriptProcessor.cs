using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Model;
using DaJet.Scripting.Model;
using System.Data;
using System.Globalization;

namespace DaJet.Scripting
{
    public static class ScriptProcessor
    {
        public static bool TryProcess(in IMetadataProvider context,
            in string script, in Dictionary<string, object> parameters,
            out TranspilerResult result, out string error)
        {
            if (context is null) { throw new ArgumentNullException(nameof(context)); }
            if (string.IsNullOrWhiteSpace(script)) { throw new ArgumentNullException(nameof(script)); }
            if (parameters is null) { throw new ArgumentNullException(nameof(parameters)); }

            result = null;

            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    return false;
                }
            }

            Dictionary<string, object> sql_parameters = new();

            foreach (var item in parameters)
            {
                sql_parameters.Add(item.Key, item.Value);
            }

            OverrideEntityParameters(in model, in sql_parameters); // set entity type codes for binding and transformation

            if (!new MetadataBinder().TryBind(model, in context, out _, out List<string> errors))
            {
                error = ExceptionHelper.FormatErrorMessage(in errors); return false;
            }

            if (!new ScriptTransformer().TryTransform(model, out error)) { return false; }

            ISqlTranspiler transpiler;

            if (context.DatabaseProvider == DatabaseProvider.Sqlite)
            {
                transpiler = new PgSqlTranspiler(); //TODO: !!!
            }
            else if (context.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                transpiler = new MsSqlTranspiler() { YearOffset = context.YearOffset };
            }
            else if (context.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                transpiler = new PgSqlTranspiler() { YearOffset = context.YearOffset };
            }
            else
            {
                error = $"Unsupported database provider: {context.DatabaseProvider}";
                return false;
            }

            if (!transpiler.TryTranspile(in model, in context, out result, out error))
            {
                return false;
            }

            ConfigureParameters(in model, in context, in sql_parameters); // prepare parameters for sql commands

            ConfigureSelectParameters(in model, in context, in sql_parameters); // execute: DECLARE @name entity = SELECT...

            ExecuteImportStatements(in model, in context, in sql_parameters); // execute: IMPORT script INTO @tvp

            ConfigureMetadataTable(in model, in context, in sql_parameters);

            result.Parameters = sql_parameters;

            return true;
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

                declare.Type.Identifier = ParserHelper.GetDataTypeLiteral(typeof(Entity));

                // entity type code provided by user determines the data type for parameter

                ScalarExpression scalar = new()
                {
                    Token = TokenType.Entity,
                    Literal = entity.ToString()
                };

                declare.Initializer = scalar; 
            }
        }
        public static void ConfigureParameters(in ScriptModel model, in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            // add script parameters to the dictionary if they are missing
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is not DeclareStatement declare) { continue; }

                if (declare.Initializer is SelectExpression) { continue; }

                if (declare.Type.Token == TokenType.Array ||
                    declare.Type.Token == TokenType.Object) { continue; }

                // 0. Database UDT parameter must be provided by the caller !!!
                
                if (declare.Type.Binding is UserDefinedType) { continue; }

                // 1. Set default value if parameter value is not initialized in script.

                if (declare.Initializer is not ScalarExpression scalar)
                {
                    scalar = ParserHelper.CreateDefaultScalar(in declare);
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
                    if (ParserHelper.IsDataType(declare.Type.Identifier, out Type type))
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
        
        public static void ConfigureSelectParameters(in ScriptModel model, in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            foreach (SyntaxNode node in model.Statements)
            {
                if (node is not DeclareStatement declare) { continue; }

                if (declare.Initializer is SelectExpression select)
                {
                    SqlStatement statement = CreateScriptStatement(in context, in select);

                    List<VariableReference> variables = new VariableReferenceExtractor().Extract(select);

                    Dictionary<string, object> select_parameters = new();

                    foreach (VariableReference variable in variables)
                    {
                        string name = variable.Identifier[1..];

                        if (parameters.TryGetValue(name, out object value))
                        {
                            select_parameters.Add(name, value);
                        }
                    }

                    if (declare.Type.Binding is Entity binding)
                    {
                        Entity entity = SelectEntityValue(in context, in statement, in select_parameters, binding.TypeCode);

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
                    else
                    {
                        object value = SelectParameterValue(in context, in statement, in select_parameters);

                        if (value is Guid uuid)
                        {
                            parameters.Add(declare.Name[1..], uuid.ToByteArray());
                        }
                        else
                        {
                            parameters.Add(declare.Name[1..], value);
                        }
                    }
                }
            }
        }
        private static SqlStatement CreateScriptStatement(in IMetadataProvider context, in SelectExpression select)
        {
            ScriptModel model = new();

            model.Statements.Add(new SelectStatement()
            {
                Expression = select
            });

            ISqlTranspiler transpiler;

            if (context.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                transpiler = new MsSqlTranspiler() { YearOffset = context.YearOffset };
            }
            else if (context.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                transpiler = new PgSqlTranspiler() { YearOffset = context.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {context.DatabaseProvider}");
            }

            if (!transpiler.TryTranspile(in model, in context, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            if (result is not null && result.Statements is not null && result.Statements.Count > 0)
            {
                return result.Statements[0];
            }

            throw new InvalidOperationException("Entity parameters configuration error");
        }
        private static Entity SelectEntityValue(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters, int typeCode)
        {
            object value = null;

            IQueryExecutor executor = context.CreateQueryExecutor(); //TODO: use OneDbConnection ?

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                value = statement.Mapper.Properties[0].GetValue(in reader); break;
            }

            if (value is Entity entity)
            {
                return entity;
            }
            else if (typeCode > 0 && value is Guid uuid)
            {
                return new Entity(typeCode, uuid);
            }

            return Entity.Undefined;
        }
        private static object SelectParameterValue(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
        {
            object value = null;

            IQueryExecutor executor = context.CreateQueryExecutor(); //TODO: use OneDbConnection ?

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                value = statement.Mapper.Properties[0].GetValue(in reader); break;
            }

            return value;
        }

        public static void ExecuteImportStatements(in ScriptModel model, in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            List<SyntaxNode> items_to_remove = new();

            foreach (SyntaxNode node in model.Statements)
            {
                if (node is ImportStatement import)
                {
                    items_to_remove.Add(node);
                    
                    ExecuteImportStatement(in import, in context, in parameters);
                }
            }

            foreach (SyntaxNode node in items_to_remove)
            {
                model.Statements.Remove(node);
            }
        }
        private static void ExecuteImportStatement(in ImportStatement import, in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            DaJetDataSource dajet = new();

            Uri uri = new(import.Source);

            if (uri.Scheme != "dajet")
            {
                throw new InvalidOperationException($"Unknown data source scheme: {uri.Scheme}");
            }

            string target = uri.Host;
            string script = uri.AbsolutePath;
            string scriptPath = target + "/" + script;

            ConfigureImportStatementParameters(in uri, in parameters, out Dictionary<string, object> importParameters);

            InfoBaseRecord database = dajet.Select<InfoBaseRecord>(target) ?? throw new ArgumentException($"Target not found: {target}");
            ScriptRecord record = dajet.Select<ScriptRecord>(scriptPath) ?? throw new ArgumentException($"Script not found: {script}");

            IMetadataProvider importContext = MetadataService.CreateOneDbMetadataProvider(in database);

            if (!TryProcess(in importContext, record.Script, in importParameters, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            List<Dictionary<string, object>> table = new();

            SqlStatement statement = result.Statements
                .Where(s => !string.IsNullOrEmpty(s.Script))
                .FirstOrDefault(); //TODO: process multiple results

            if (statement is not null)
            {
                IQueryExecutor executor = importContext.CreateQueryExecutor(); //TODO: use OneDbConnection ?

                foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, importParameters))
                {
                    Dictionary<string, object> entity = new();

                    foreach (PropertyMapper property in statement.Mapper.Properties)
                    {
                        entity.Add(property.Name, property.GetValue(in reader));
                    }

                    table.Add(entity);
                }
            }

            if (import.Target is not null && import.Target.Count > 0)
            {
                VariableReference variable = import.Target[0];

                string key = variable.Identifier[1..];

                if (variable.Binding is UserDefinedType type)
                {
                    parameters[key] = new TableValuedParameter()
                    {
                        Name = key,
                        Value = table,
                        DbName = type.Name
                    };
                }
            }
        }
        private static void ConfigureImportStatementParameters(in Uri scriptUri, in Dictionary<string, object> parameters, out Dictionary<string, object> importParameters)
        {
            importParameters = new Dictionary<string, object>();

            string query = scriptUri.Query;

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.TrimStart('?');

                string[] queryParameters = query.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                foreach (string parameter in queryParameters)
                {
                    string[] replace = parameter.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    string parameterName = replace[0];
                    string variableName = (replace.Length > 1) ? replace[1] : parameterName;

                    if (parameters.TryGetValue(variableName, out object value))
                    {
                        importParameters.Add(parameterName, value);
                    }
                }
            }
        }

        private static void ConfigureMetadataTable(in ScriptModel model, in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            List<TableReference> tables = new MetadataTableExtractor().Extract(model);

            if (tables is null || tables.Count == 0) { return; }

            foreach (TableReference table in tables)
            {
                if (table.Identifier == "Метаданные.Объекты")
                {
                    ConfigureMetadataTable_Object(in context, in parameters);
                }
                else if (table.Identifier == "Метаданные.Свойства")
                {
                    ConfigureMetadataTable_Property(in context, in parameters);
                }
            }
        }
        private static void ConfigureMetadataTable_Object(in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            List<Dictionary<string, object>> table = new();

            GetMetadataTables(MetadataTypes.Catalog, in context, in table);
            GetMetadataTables(MetadataTypes.Document, in context, in table);
            GetMetadataTables(MetadataTypes.InformationRegister, in context, in table);
            GetMetadataTables(MetadataTypes.AccumulationRegister, in context, in table);

            parameters["md_object"] = new TableValuedParameter()
            {
                Name = "md_object",
                Value = table,
                DbName = "dajet_md_object" //NOTE: prefix 'dajet' is very important for PostgreSQL !!!
            };
        }
        private static void GetMetadataTables(Guid type, in IMetadataProvider context, in List<Dictionary<string, object>> table)
        {
            foreach (MetadataItem item in context.GetMetadataItems(type))
            {
                MetadataObject metadata = context.GetMetadataObject(item.Type, item.Uuid);

                if (metadata is ApplicationObject entity)
                {
                    Dictionary<string, object> record = new()              // CREATE TYPE dajet_md_object
                    {                                                      // (
                        { "Ссылка",   entity.Uuid },                       //   Ссылка   uuid,
                        { "Код",      new decimal(entity.TypeCode) },      //   Код      number(5),
                        { "Тип",      MetadataTypes.ResolveNameRu(type) }, //   Тип      string(32),
                        { "Имя",      entity.Name },                       //   Имя      string(128),
                        { "Таблица",  entity.TableName },                  //   Таблица  string(128),
                        { "Владелец", Guid.Empty }                         //   Владелец uuid
                    };                                                     // )
                    table.Add(record);                                     // Таблица "Метаданные.Объекты"

                    if (entity is ITablePartOwner owner)
                    {
                        foreach (TablePart tablePart in owner.TableParts)
                        {
                            record = new Dictionary<string, object>()
                            {
                                { "Ссылка",   tablePart.Uuid },
                                { "Код",      new decimal(tablePart.TypeCode) },
                                { "Тип",      "ТабличнаяЧасть" },
                                { "Имя",      tablePart.Name },
                                { "Таблица",  tablePart.TableName },
                                { "Владелец", entity.Uuid }
                            };
                            table.Add(record);
                        }
                    }
                }
            }
        }
        private static void ConfigureMetadataTable_Property(in IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            List<Dictionary<string, object>> table = new();

            Dictionary<string, object> entity = new();

            entity.Add("__oid_U", Guid.Empty);
            entity.Add("__type_U", Guid.Empty);
            entity.Add("__code_N", 12M);
            entity.Add("__name_S", "test");
            entity.Add("__table_S", "_table12");
            entity.Add("__owner_U", Guid.Empty);

            table.Add(entity);

            parameters["md_object"] = new TableValuedParameter()
            {
                Name = "md_object",
                Value = table,
                DbName = "udt_md_object"
            };
        }

        public static bool TryBind(in ScriptModel script, in IMetadataProvider database, out string error)
        {
            if (!new MetadataBinder().TryBind(script, in database, out _, out List<string> errors))
            {
                error = ExceptionHelper.FormatErrorMessage(in errors); return false;
            }

            if (!new ScriptTransformer().TryTransform(script, out error)) { return false; }

            return true;
        }
    }
}