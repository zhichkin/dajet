using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System.Text;

namespace DaJet.Data.Client
{
    public static class MetadataProviderExtensions
    {
        public static DataObject Create(this IMetadataProvider context, in string metadataName)
        {
            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException(metadataName);
            }

            return Create(in entity, in metadataName);
        }
        private static DataObject Create(in ApplicationObject metadata, in string metadataName)
        {
            int code = metadata.TypeCode;
            string name = metadataName;
            int capacity = metadata.Properties.Count;

            if (metadata is TablePart)
            {
                --capacity; // Скрытое поле базы данных "_KeyField"
            }
            else if (metadata is IPredefinedValueOwner)
            {
                --capacity; // Виртуальный реквизит "Предопределённый"
            }

            ITablePartOwner aggregate = metadata as ITablePartOwner;

            if (aggregate is not null)
            {
                capacity += aggregate.TableParts.Count;
            }

            DataObject entity = new(capacity);
            entity.SetCodeAndName(code, name);

            MetadataProperty property;

            for (int i = 0; i < metadata.Properties.Count; i++)
            {
                property = metadata.Properties[i];

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

                //if (metadata is AccumulationRegister register &&
                //    register.RegisterKind == RegisterKind.Balance)
                //{
                //    if (property.Purpose == PropertyPurpose.System
                //        && property.Name == "ВидДвижения")
                //    {

                //    }
                //}

                object value = property.PropertyType.GetDefaultValue();

                entity.SetValue(property.Name, value);
            }

            if (aggregate is not null)
            {
                foreach (TablePart table in aggregate.TableParts)
                {
                    entity.SetValue(table.Name, new List<DataObject>());
                }
            }

            return entity;
        }

        public static DataObject Select(this IMetadataProvider context, Entity entity)
        {
            int typeCode = entity.TypeCode;

            MetadataItem item = context.GetMetadataItem(typeCode);

            if (item == MetadataItem.Empty)
            {
                throw new InvalidOperationException($"Reference type code not found [{typeCode}]");
            }

            string typeName = item.ToString();

            MetadataObject metadata = context.GetMetadataObject(item.Type, item.Uuid)
                ?? throw new InvalidOperationException($"Metadata object not found [{typeCode}:{typeName}]");

            DataObject root = null; // reference object

            string script = GenerateSelectEntityScript(in metadata, in typeName);

            using (OneDbConnection connection = new(context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;
                    command.Parameters.Add("Ссылка", entity);

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read()) // reference object main table
                        {
                            root = new DataObject(reader.FieldCount); //FIXME: capacity + table parts count

                            root.SetCodeAndName(typeCode, typeName);

                            reader.Map(in root);

                            //THINK: root.Remove("ВерсияДанных");
                        }

                        while (reader.NextResult()) // table parts of the reference object
                        {
                            List<DataObject> table = new();

                            while (reader.Read())
                            {
                                DataObject record = new(reader.FieldCount);

                                reader.Map(in record);

                                table.Add(record);
                            }

                            root.SetValue(reader.Mapper.Name, table); //FIXME: this increments capacity of the root
                        }

                        reader.Close();
                    }
                }
            }

            return root;
        }
        private static string GenerateSelectEntityScript(in MetadataObject metadata, in string metadataName)
        {
            if (metadata is not ApplicationObject entity)
            {
                throw new InvalidOperationException($"Metadata object not supported [{metadataName}]");
            }

            StringBuilder script = new();
            
            script.AppendLine($"DECLARE @Ссылка {metadataName};");
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
            }

            return script.ToString();
        }

        public static void Insert(this IMetadataProvider context, in DataObject record)
        {
            string metadataName = record.GetName();

            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException(metadataName);
            }

            string script = GenerateInsertScript(in entity, in record);

            using (OneDbConnection connection = new(context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;

                    for (int i = 0; i < record.Count(); i++)
                    {
                        command.Parameters.Add(record.GetName(i), record.GetValue(i));
                    }

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        private static string GenerateInsertScript(in ApplicationObject metadata, in DataObject record)
        {
            string metadataName = record.GetName();

            StringBuilder script = new();
            List<string> assignments = new();

            bool predefined = metadata is IPredefinedValueOwner;

            MetadataProperty property;
            int count = metadata.Properties.Count;

            for (int i = 0; i < count; i++)
            {
                property = metadata.Properties[i];

                if (!record.Contains(property.Name))
                {
                    continue;
                }

                if (predefined
                    && property.Purpose == PropertyPurpose.System
                    && property.Name == "Предопределённый")
                {
                    continue;
                }

                string typeName = property.PropertyType.GetDataTypeLiteral();
                script.AppendLine($"DECLARE @{property.Name} {typeName};");
                assignments.Add($"@{property.Name} AS {property.Name}");
            }

            script.AppendLine($"INSERT {metadataName} FROM");
            script.AppendLine("(SELECT");

            count = assignments.Count;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    script.Append(',').AppendLine();
                }
                script.Append(assignments[i]);
            }
            script.AppendLine().AppendLine(") AS source");

            return script.ToString();
        }

        public static void Update(this IMetadataProvider context, in DataObject filter, in DataObject values)
        {
            string metadataName = filter.GetName();

            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException(metadataName);
            }

            string script = GenerateUpdateScript(in entity, in filter, in values);

            using (OneDbConnection connection = new(context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;

                    for (int i = 0; i < filter.Count(); i++)
                    {
                        command.Parameters.Add("f_" + filter.GetName(i), filter.GetValue(i));
                    }

                    for (int i = 0; i < values.Count(); i++)
                    {
                        command.Parameters.Add("v_" + values.GetName(i), values.GetValue(i));
                    }

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        private static string GenerateUpdateScript(in ApplicationObject metadata, in DataObject filter, in DataObject values)
        {
            string metadataName = filter.GetName();

            StringBuilder script = new();
            List<string> where = new();
            List<string> assignments = new();

            MetadataProperty property;
            int count = metadata.Properties.Count;

            for (int i = 0; i < count; i++)
            {
                property = metadata.Properties[i];

                string propertyName = property.Name;
                string typeName = property.PropertyType.GetDataTypeLiteral();

                if (filter.Contains(propertyName))
                {
                    script.AppendLine($"DECLARE @f_{propertyName} {typeName};");
                    where.Add($"{propertyName} = @f_{propertyName}");
                }

                if (values.Contains(propertyName))
                {
                    script.AppendLine($"DECLARE @v_{propertyName} {typeName};");
                    assignments.Add($"{propertyName} = @v_{propertyName}");
                }
            }

            script.AppendLine($"UPDATE {metadataName}");
            script.AppendLine("WHERE");

            count = where.Count;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    script.Append(" AND").AppendLine();
                }
                script.Append(where[i]);
            }

            script.AppendLine().AppendLine("SET");

            count = assignments.Count;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    script.Append(',').AppendLine();
                }
                script.Append(assignments[i]);
            }

            return script.ToString();
        }

        public static void Delete(this IMetadataProvider context, in DataObject filter)
        {
            string metadataName = filter.GetName();

            MetadataObject metadata = context.GetMetadataObject(metadataName);

            if (metadata is not ApplicationObject entity)
            {
                throw new NotSupportedException(metadataName);
            }

            string script = GenerateDeleteScript(in entity, in filter);

            using (OneDbConnection connection = new(context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;

                    for (int i = 0; i < filter.Count(); i++)
                    {
                        command.Parameters.Add(filter.GetName(i), filter.GetValue(i));
                    }

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        private static string GenerateDeleteScript(in ApplicationObject metadata, in DataObject filter)
        {
            string metadataName = filter.GetName();

            StringBuilder script = new();
            List<string> where = new();

            MetadataProperty property;
            int count = metadata.Properties.Count;

            for (int i = 0; i < count; i++)
            {
                property = metadata.Properties[i];

                string propertyName = property.Name;
                string typeName = property.PropertyType.GetDataTypeLiteral();

                if (filter.Contains(propertyName))
                {
                    script.AppendLine($"DECLARE @{propertyName} {typeName};");
                    where.Add($"{propertyName} = @{propertyName}");
                }
            }

            script.AppendLine($"DELETE {metadataName} WHERE");

            count = where.Count;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    script.Append(" AND").AppendLine();
                }
                script.Append(where[i]);
            }

            return script.ToString();
        }
    }
}