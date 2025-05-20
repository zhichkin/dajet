using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata
{
    public sealed class MetadataObjectConverter
    {
        private readonly OneDbMetadataProvider _provider;
        public MetadataObjectConverter(in OneDbMetadataProvider provider)
        {
            _provider = provider;
        }
        public DataObject Convert(in MetadataObject metadata)
        {
            if (metadata is Account account) { return Convert(in account); }
            else if (metadata is Catalog catalog) { return Convert(in catalog); }
            else if (metadata is Document document) { return Convert(in document); }
            else if (metadata is Constant constant) { return Convert(in constant); }
            else if (metadata is Enumeration enumeration) { return Convert(in enumeration); }
            else if (metadata is Publication publication) { return Convert(in publication); }
            else if (metadata is Characteristic characteristic) { return Convert(in characteristic); }
            else if (metadata is SharedProperty property) { return Convert(in property); }
            else if (metadata is AccountingRegister register1) { return Convert(in register1); }
            else if (metadata is InformationRegister register2) { return Convert(in register2); }
            else if (metadata is AccumulationRegister register3) { return Convert(in register3); }
            else if (metadata is NamedDataTypeDescriptor descriptor) { return Convert(in descriptor); }

            return null; // Unsupported metadata type
        }
        private DataObject Convert(in ApplicationObject metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Code", metadata.TypeCode);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("Alias", metadata.Alias);
            @object.SetValue("Table", metadata.TableName);
            @object.SetValue("FullName", fullName);
            @object.SetValue("Properties", Convert(metadata.Properties));

            if (metadata is ITablePartOwner owner)
            {
                @object.SetValue("TableParts", Convert(owner.TableParts, in fullName));
            }
            else
            {
                @object.SetValue("TableParts", new List<DataObject>(0));
            }
            
            return @object;
        }
        private DataObject Convert(in Constant metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("FullName", fullName);
            @object.SetValue("DataType", Convert(metadata.DataTypeDescriptor));
            @object.SetValue("References", ResolveReferences(metadata.References));

            return @object;
        }
        private DataObject Convert(in SharedProperty metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("FullName", fullName);
            @object.SetValue("DbName", metadata.DbName);
            @object.SetValue("PropertyType", Convert(metadata.PropertyType));
            @object.SetValue("References", ResolveReferences(metadata.References));

            return @object;
        }
        private DataObject Convert(in NamedDataTypeDescriptor metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("FullName", fullName);
            @object.SetValue("DataType", Convert(metadata.DataTypeDescriptor));
            @object.SetValue("References", ResolveReferences(metadata.References));

            return @object;
        }
        
        private DataObject Convert(in Account metadata)
        {
            return Convert(metadata as ApplicationObject);
        }
        private DataObject Convert(in Catalog metadata)
        {
            return Convert(metadata as ApplicationObject);
        }
        private DataObject Convert(in Document metadata)
        {
            return Convert(metadata as ApplicationObject);
        }
        private DataObject Convert(in Enumeration metadata)
        {
            DataObject @object = Convert(metadata as ApplicationObject);

            List<DataObject> values = new(metadata.Values.Count);

            foreach (EnumValue item in metadata.Values)
            {
                DataObject value = new();
                value.SetValue("Name", item.Name);
                value.SetValue("Uuid", item.Uuid);
                value.SetValue("Alias", item.Alias);
                values.Add(value);
            }

            @object.SetValue("Values", values);

            return @object;
        }
        private DataObject Convert(in Publication metadata)
        {
            DataObject @object = Convert(metadata as ApplicationObject);

            List<DataObject> articles = ResolvePublicationArticles(in metadata);

            @object.SetValue("Articles", articles);

            return @object;
        }
        private DataObject Convert(in Characteristic metadata)
        {
            return Convert(metadata as ApplicationObject);
        }
        private DataObject Convert(in AccountingRegister metadata)
        {
            return Convert(metadata as ApplicationObject);
        }
        private DataObject Convert(in InformationRegister metadata)
        {
            return Convert(metadata as ApplicationObject);
        }
        private DataObject Convert(in AccumulationRegister metadata)
        {
            return Convert(metadata as ApplicationObject);
        }

        private DataObject Convert(in TablePart table, in string ownerFullName)
        {
            DataObject @object = new();

            @object.SetValue("Type", "ТабличнаяЧасть");
            @object.SetValue("Code", table.TypeCode);
            @object.SetValue("Uuid", table.Uuid);
            @object.SetValue("Name", table.Name);
            @object.SetValue("Alias", table.Alias);
            @object.SetValue("Table", table.TableName);
            @object.SetValue("FullName", $"{ownerFullName}.{table.Name}");
            @object.SetValue("Properties", Convert(table.Properties));

            return @object;
        }
        private DataObject Convert(in MetadataColumn column)
        {
            DataObject @object = new(4);

            @object.SetValue("Name", column.Name);
            @object.SetValue("Type", column.TypeName);
            @object.SetValue("IsNullable", column.IsNullable);
            @object.SetValue("Purpose", column.Purpose.GetNameRu());

            return @object;
        }
        private DataObject Convert(in MetadataProperty property)
        {
            DataObject @object = new(8);

            @object.SetValue("Name", property.Name);
            @object.SetValue("Uuid", property.Uuid);
            @object.SetValue("Alias", property.Alias);
            @object.SetValue("Purpose", property.Purpose.GetNameRu());
            @object.SetValue("Columns", Convert(property.Columns));
            @object.SetValue("DataType", Convert(property.PropertyType));
            @object.SetValue("References", ResolveReferences(property.References));

            return @object;
        }
        private List<DataObject> ResolveReferences(in List<Guid> references)
        {
            List<MetadataItem> items = _provider.ResolveReferencesToMetadataItems(references);

            List<DataObject> list = new(items.Count);

            foreach (MetadataItem item in items)
            {
                string typeName = MetadataTypes.ResolveNameRu(item.Type);

                DataObject @object = new(5);
                @object.SetValue("Uuid", item.Uuid);
                @object.SetValue("Type", typeName);

                if (item.Uuid == Guid.Empty) // Общий ссылочный тип
                {
                    @object.SetValue("Code", 0);
                    @object.SetValue("Name", item.Name);
                    @object.SetValue("FullName", item.Name);
                }
                else // Конкретный ссылочный тип
                {
                    string fullName = $"{typeName}.{item.Name}";
                    @object.SetValue("Name", item.Name);
                    @object.SetValue("FullName", fullName);

                    if (_provider.TryGetDbName(item.Uuid, out DbName dbn))
                    {
                        @object.SetValue("Code", dbn.Code); // код типа объекта метаданных
                    }
                    else
                    {
                        @object.SetValue("Code", 0);

                        //FileLogger.Default.Write($"[ERROR] REFERENCE type code is null [{item.Type}] {{{item.Uuid}}} \"{item.Name}\"");
                    }
                }

                if (@object.GetValue("Name") is string test && string.IsNullOrWhiteSpace(test))
                {
                    //FileLogger.Default.Write($"[ERROR] REFERENCE type name is null [{item.Type}] {{{item.Uuid}}} \"{item.Name}\"");
                }

                list.Add(@object);
            }

            return list;
        }
        private List<DataObject> Convert(in List<MetadataColumn> columns)
        {
            List<DataObject> list = new(columns.Count);

            foreach (MetadataColumn column in columns)
            {
                list.Add(Convert(in column));
            }

            return list;
        }
        private List<DataObject> Convert(in List<MetadataProperty> properties)
        {
            List<DataObject> list = new(properties.Count);

            foreach (MetadataProperty property in properties)
            {
                list.Add(Convert(in property));
            }

            return list;
        }
        private List<DataObject> Convert(in List<TablePart> tables, in string ownerFullName)
        {
            List<DataObject> list = new(tables.Count);

            foreach (TablePart table in tables)
            {
                list.Add(Convert(in table, in ownerFullName));
            }

            return list;
        }
        private string GetMetadataObjectName(int code)
        {
            MetadataItem item = _provider.GetMetadataItem(code);

            if (item == MetadataItem.Empty)
            {
                return "Ссылка"; // Любая ссылка
            }

            string typeName = MetadataTypes.ResolveNameRu(item.Type);

            return $"{typeName}.{item.Name}";
        }
        private List<DataObject> Convert(in DataTypeDescriptor descriptor)
        {
            List<DataObject> dataTypes = new();

            if (descriptor is null || descriptor.IsUndefined)
            {
                DataObject type = new(1);
                type.SetValue("Type", "Неопределено");
                dataTypes.Add(type);
            }
            else if (descriptor.IsUuid)
            {
                DataObject type = new(1);
                type.SetValue("Type", "УникальныйИдентификатор");
                dataTypes.Add(type);
            }
            else if (descriptor.IsValueStorage)
            {
                DataObject type = new(1);
                type.SetValue("Type", "ХранилищеЗначения");
                dataTypes.Add(type);
            }
            else if (descriptor.IsBinary)
            {
                DataObject type = new(1);
                type.SetValue("Type", "ДвоичныеДанные");
                dataTypes.Add(type);
            }
            else if (descriptor.IsUnionType(out bool canBeSimple, out bool canBeReference))
            {
                List<string> union = new();

                if (canBeSimple)
                {
                    if (descriptor.CanBeBoolean)
                    {
                        DataObject type = new(1);
                        type.SetValue("Type", "Булево");
                        dataTypes.Add(type);
                    }

                    if (descriptor.CanBeNumeric)
                    {
                        DataObject type = new(1);
                        type.SetValue("Type", $"Число({descriptor.NumericPrecision},{descriptor.NumericScale})");
                        dataTypes.Add(type);
                    }

                    if (descriptor.CanBeDateTime)
                    {
                        DataObject type = new(1);
                        if (descriptor.DateTimePart == DateTimePart.Date)
                        {
                            type.SetValue("Type", "Дата");
                        }
                        else if (descriptor.DateTimePart == DateTimePart.Time)
                        {
                            type.SetValue("Type", "Время");
                        }
                        else
                        {
                            type.SetValue("Type", "ДатаВремя");
                        }
                        dataTypes.Add(type);
                    }

                    if (descriptor.CanBeString)
                    {
                        DataObject type = new(1);
                        type.SetValue("Type", $"Строка({descriptor.StringLength},{descriptor.StringKind.GetNameRu()})");
                        dataTypes.Add(type);
                    }
                }

                if (canBeReference)
                {
                    DataObject type = new(1);
                    type.SetValue("Type", GetMetadataObjectName(descriptor.TypeCode)); // descriptor.Reference
                    dataTypes.Add(type);
                }
            }
            else if (descriptor.CanBeBoolean)
            {
                DataObject type = new(1);
                type.SetValue("Type", "Булево");
                dataTypes.Add(type);
            }
            else if (descriptor.CanBeNumeric)
            {
                DataObject type = new(1);
                type.SetValue("Type", $"Число({descriptor.NumericPrecision},{descriptor.NumericScale})");
                dataTypes.Add(type);
            }
            else if (descriptor.CanBeDateTime)
            {
                DataObject type = new(1);
                if (descriptor.DateTimePart == DateTimePart.Date)
                {
                    type.SetValue("Type", "Дата");
                }
                else if (descriptor.DateTimePart == DateTimePart.Time)
                {
                    type.SetValue("Type", "Время");
                }
                else
                {
                    type.SetValue("Type", "ДатаВремя");
                }
                dataTypes.Add(type);
            }
            else if (descriptor.CanBeString)
            {
                DataObject type = new(1);
                type.SetValue("Type", $"Строка({descriptor.StringLength},{descriptor.StringKind.GetNameRu()})");
                dataTypes.Add(type);
            }
            else if (descriptor.CanBeReference)
            {
                DataObject type = new(1);
                type.SetValue("Type", GetMetadataObjectName(descriptor.TypeCode)); // descriptor.Reference
                dataTypes.Add(type);
            }

            return dataTypes;
        }
        private List<DataObject> ResolvePublicationArticles(in Publication publication)
        {
            List<DataObject> articles = new();

            List<Guid> types = new();
            types.AddRange(MetadataTypes.ValueObjectTypes);
            types.AddRange(MetadataTypes.ReferenceObjectTypes);

            foreach (var article in publication.Articles)
            {
                foreach (Guid type in types)
                {
                    int code = 0;
                    Guid uuid = article.Key;
                    string name = _provider.GetMetadataName(type, uuid);
                    
                    if (_provider.TryGetDbName(article.Key, out DbName dbname))
                    {
                        code = dbname.Code;
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    DataObject @object = ConvertArticle(type, uuid, code, in name, article.Value);

                    articles.Add(@object);
                    
                    break;
                }
            }

            return articles;
        }
        private DataObject ConvertArticle(Guid type, Guid uuid, int code, in string name, AutoPublication auto)
        {
            DataObject @object = new(7); // +1 свойство для состава плана обмена

            string typeName = MetadataTypes.ResolveNameRu(type);

            @object.SetValue("Type", typeName);
            @object.SetValue("Uuid", uuid);
            @object.SetValue("Code", code);
            @object.SetValue("Name", name);
            @object.SetValue("FullName", $"{typeName}.{name}");
            @object.SetValue("AutoChangeTracking", auto == AutoPublication.Allow); // Авторегистрация

            return @object;
        }
    }
}