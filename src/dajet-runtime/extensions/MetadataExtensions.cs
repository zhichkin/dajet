using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;
using System.Data.Common;
using System.Text;

namespace DaJet.Runtime
{
    public static class MetadataExtensions
    {
        public static DataObject ToDataObject(this MetadataObject metadata)
        {
            DataObject @object = new(7);

            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());

            if (metadata is TablePart table)
            {
                typeName = "ТабличнаяЧасть";
            }

            @object.SetValue("Ссылка", metadata.Uuid);
            @object.SetValue("Код", 0);
            @object.SetValue("Тип", typeName);
            @object.SetValue("Имя", metadata.Name);
            @object.SetValue("ПолноеИмя", $"{typeName}.{metadata.Name}");
            @object.SetValue("Таблица", string.Empty);
            @object.SetValue("Владелец", Guid.Empty);

            if (metadata is ApplicationObject entity)
            {
                @object.SetValue("Код", entity.TypeCode);
                @object.SetValue("Таблица", entity.TableName);
            }

            return @object;
        }
        public static DataObject CreateObject(this IMetadataProvider context, in string metadataName)
        {
            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException($"Metadata not found [{metadataName}]");
            }

            int code = entity.TypeCode;
            string name = metadataName;
            int capacity = entity.Properties.Count;

            if (entity is TablePart)
            {
                --capacity; // Скрытое поле базы данных "_KeyField"
            }
            else if (metadata is IPredefinedValueOwner)
            {
                --capacity; // Виртуальный реквизит "Предопределённый"
            }

            ITablePartOwner aggregate = entity as ITablePartOwner;

            if (aggregate is not null)
            {
                capacity += aggregate.TableParts.Count;
            }

            DataObject _object = new(capacity);
            _object.SetCodeAndName(code, name);

            MetadataProperty property;

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                property = entity.Properties[i];

                if (property.Purpose == PropertyPurpose.System)
                {
                    if (aggregate is null) // may be TablePart
                    {
                        if (property.Name == "KeyField")
                        {
                            continue;
                        }
                    }
                    else  // Reference object - TablePart owner
                    {
                        if (property.Name == "Предопределённый")
                        {
                            continue;
                        }
                    }
                }

                object value = property.PropertyType.GetDefaultValue();

                _object.SetValue(property.Name, value);
            }

            if (aggregate is not null)
            {
                foreach (TablePart table in aggregate.TableParts)
                {
                    _object.SetValue(table.Name, new List<DataObject>());
                }
            }

            return _object;
        }
        public static DataObject SelectObject(this IMetadataProvider context, in Entity key)
        {
            MetadataItem item = context.GetMetadataItem(key.TypeCode);

            if (item == MetadataItem.Empty)
            {
                throw new InvalidOperationException($"Reference type code not found [{key.TypeCode}]");
            }

            MetadataObject metadata = context.GetMetadataObject(item.Type, item.Uuid);

            string type = MetadataTypes.ResolveNameRu(item.Type);

            string metadataName =  $"{type}.{metadata.Name}";

            return context.SelectObject(in metadataName, in key);
        }
        public static DataObject SelectObject(this IMetadataProvider context, in string metadataName, in Entity key)
        {
            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException();
            }

            if (key.TypeCode != entity.TypeCode)
            {
                throw new ArgumentException($"Type code mismatch: {metadataName} [{entity.TypeCode}]<>[{key.TypeCode}]");
            }

            string sourceCode = GenerateSelectEntityScript(in metadata, in metadataName);
            ScriptModel script = context.CompileAndBindScript(in sourceCode);
            TranspilerResult sql = context.TranspileScript(in script);

            Dictionary<string, object> parameters = new()
            {
                { "@UUIDOF_Ссылка", key.Identity }
            };
            
            DataObject _object = SelectDataObject(in context, in sql, in parameters);

            _object?.SetCodeAndName(entity.TypeCode, metadataName);
            
            return _object;
        }
        public static DataObject SelectObject(this IMetadataProvider context, in string metadataName, in DataObject key)
        {
            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException();
            }

            string sourceCode = GenerateSelectRecordScript(in metadata, in metadataName, in key);
            ScriptModel script = context.CompileAndBindScript(in sourceCode);
            TranspilerResult sql = context.TranspileScript(in script);

            Dictionary<string, object> parameters = new();

            for (int i = 0; i < key.Count(); i++)
            {
                string name = key.GetName(i);
                object value = key.GetValue(i);

                if (value is Union.CaseEntity union) //TODO: надо много думать ...
                {
                    value = union.Value;
                }

                parameters.Add(name, value);
            }

            foreach (SqlStatement statement in sql.Statements)
            {
                if (statement.Node is DeclareStatement)
                {
                    continue;
                }

                //List<string> parametersToRemove = new();

                foreach (FunctionDescriptor function in statement.Functions)
                {
                    if (function.Node.Name == UDF_TYPEOF.Name)
                    {
                        if (function.Node.Parameters[0] is VariableReference variable)
                        {
                            string parameterName = variable.Identifier.TrimStart('@');
                            _ = parameters.TryGetValue(parameterName, out object value);
                            if (value is Entity entityValue)
                            {
                                _ = parameters.TryAdd($"TYPEOF_{parameterName}", entityValue.TypeCode);
                            }
                        }
                    }
                    else if (function.Node.Name == UDF_UUIDOF.Name)
                    {
                        if (function.Node.Parameters[0] is VariableReference variable)
                        {
                            string parameterName = variable.Identifier.TrimStart('@');
                            _ = parameters.TryGetValue(parameterName, out object value);
                            if (value is Entity entityValue)
                            {
                                _ = parameters.TryAdd($"UUIDOF_{parameterName}", entityValue.Identity);
                            }
                        }
                    }
                }
            }

            DataObject _object = SelectDataObject(in context, in sql, in parameters);

            _object?.SetCodeAndName(entity.TypeCode, metadataName);

            return _object;
        }
        
        private static string GenerateSelectEntityScript(in MetadataObject metadata, in string metadataName)
        {
            if (metadata is not ApplicationObject entity)
            {
                throw new InvalidOperationException($"Metadata object not supported [{metadataName}]");
            }

            StringBuilder script = new();

            script.AppendLine($"DECLARE @Ссылка entity;");
            script.AppendLine();
            script.AppendLine("SELECT");

            int line = 0;

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                MetadataProperty property = entity.Properties[i];

                if (property.Purpose == PropertyPurpose.System && property.Name == "Предопределённый")
                {
                    continue;
                }

                if (line > 0)
                {
                    script.AppendLine(",");
                }
                script.Append(property.Name); line++;
            }
            script.AppendLine();
            script.AppendLine($"FROM {metadataName}");
            script.AppendLine("WHERE Ссылка = @Ссылка");

            if (metadata is not ITablePartOwner aggregate)
            {
                return script.ToString();
            }

            foreach (TablePart table in aggregate.TableParts)
            {
                line = 0;
                script.AppendLine();
                script.AppendLine("SELECT");

                for (int i = 0; i < table.Properties.Count; i++)
                {
                    MetadataProperty property = table.Properties[i];

                    if (property.Name == "KeyField") { continue; }

                    if (line > 0)
                    {
                        script.AppendLine(",");
                    }
                    script.Append(property.Name); line++;
                }
                script.AppendLine();
                script.AppendLine($"FROM {metadataName}.{table.Name} AS {table.Name}");
                script.AppendLine("WHERE Ссылка = @Ссылка");
                script.AppendLine("ORDER BY Ссылка ASC, KeyField ASC");
            }

            return script.ToString();
        }
        private static string GenerateSelectRecordScript(in MetadataObject metadata, in string metadataName, in DataObject key)
        {
            if (metadata is not ApplicationObject entity)
            {
                throw new InvalidOperationException($"Metadata object not supported [{metadataName}]");
            }

            StringBuilder script = new();

            for (int i = 0; i < key.Count(); i++)
            {
                string name = key.GetName(i);
                object value = key.GetValue(i);

                if (value is null)
                {
                    throw new InvalidOperationException($"Key \"{name}\" value is NULL");
                }

                Type type = value.GetType();

                if (value is Union.CaseEntity union)
                {
                    type = typeof(Entity);
                    value = union.Value;
                }

                string typeName = ParserHelper.GetDataTypeLiteral(type);

                script.AppendLine($"DECLARE @{name} {typeName};");
            }
            
            script.AppendLine();
            script.AppendLine("SELECT");

            int line = 0;

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                MetadataProperty property = entity.Properties[i];

                if (property.Purpose == PropertyPurpose.System && property.Name == "Предопределённый")
                {
                    continue;
                }

                if (line > 0)
                {
                    script.AppendLine(",");
                }
                script.Append(property.Name); line++;
            }
            script.AppendLine();
            script.AppendLine($"FROM {metadataName}");
            script.AppendLine("WHERE");

            for (int i = 0; i < key.Count(); i++)
            {
                string name = key.GetName(i);

                if (i > 0) { script.Append("AND "); }

                script.AppendLine($"{name} = @{name}");
            }

            return script.ToString();
        }
        private static ScriptModel CompileAndBindScript(this IMetadataProvider context, in string sourceCode)
        {
            if (!new ScriptParser().TryParse(in sourceCode, out ScriptModel script, out string error))
            {
                throw new InvalidOperationException(error);
            }

            if (!ScriptProcessor.TryBind(in script, in context, out error))
            {
                throw new InvalidOperationException(error);
            }

            return script;
        }
        private static TranspilerResult TranspileScript(this IMetadataProvider context, in ScriptModel script)
        {
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

            if (!transpiler.TryTranspile(in script, in context, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            return result;
        }
        
        private static DataObject SelectDataObject(in IMetadataProvider context, in TranspilerResult sql, in Dictionary<string, object> parameters)
        {
            DataObject _object = null;

            int mapperIndex = 0;
            EntityMapper mapper;

            for (int i = 0; i < sql.Statements.Count; i++)
            {
                if (sql.Statements[i].Node is not DeclareStatement)
                {
                    mapperIndex = i; break;
                }
            }

            IDbConnectionFactory factory = DbConnectionFactory.GetFactory(context.DatabaseProvider);

            using (DbConnection connection = factory.Create(context.ConnectionString))
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql.SqlScript;

                    factory.ConfigureParameters(in command, in parameters, context.YearOffset);

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        mapper = sql.Statements[mapperIndex].Mapper;

                        if (reader.Read()) // reference object main table
                        {
                            _object = new DataObject(reader.FieldCount); //FIXME: capacity + table parts count

                            mapper.Map(in reader, in _object);

                            while (reader.NextResult()) // table parts of the reference object
                            {
                                mapper = sql.Statements[++mapperIndex].Mapper;

                                List<DataObject> table = new();

                                while (reader.Read())
                                {
                                    DataObject record = new(reader.FieldCount);

                                    mapper.Map(in reader, in record);

                                    table.Add(record);
                                }

                                _object.SetValue(mapper.Name, table); //FIXME: this increments capacity of the root
                            }
                        }
                        reader.Close();
                    }
                }
            }

            return _object;
        }
    }
}