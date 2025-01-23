using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;

namespace DaJet.Runtime
{
    public sealed class MetadataExporter : UserDefinedProcessor
    {
        private IDbConnectionFactory _factory;
        private OneDbMetadataProvider _provider;
        public MetadataExporter(in ScriptScope scope) : base(scope) { }
        public override void Process() { Execute(); _next?.Process(); }
        private void Execute()
        {
            _ = _scope.TryGetValue(_statement.Variables[0].Identifier, out object value1);

            if (value1 is not string source)
            {
                throw new ArgumentException($"[{nameof(MetadataExporter)}] source database is not defined");
            }

            Uri uri = _scope.GetUri(source);

            _factory = DbConnectionFactory.GetFactory(in uri);

            OneDbMetadataProviderOptions options = new()
            {
                UseExtensions = false,
                ResolveReferences = true,
                ConnectionString = DbConnectionFactory.GetConnectionString(in uri)
            };

            if (uri.Scheme == "mssql")
            {
                options.DatabaseProvider = DatabaseProvider.SqlServer;
            }
            else if (uri.Scheme == "pgsql")
            {
                options.DatabaseProvider = DatabaseProvider.PostgreSql;
            }
            else
            {
                throw new NotSupportedException($"[{nameof(MetadataExporter)}] database {uri.Scheme} is not supported");
            }

            if (!OneDbMetadataProvider.TryCreateMetadataProvider(in options, out _provider, out string error))
            {
                FileLogger.Default.Write(error);
            }

            _ = _scope.TryGetValue(_statement.Variables[1].Identifier, out object value2);

            if (value2 is not string metadataName)
            {
                throw new ArgumentException($"[{nameof(MetadataExporter)}] metadata name is missing");
            }

            MetadataObject metadata = _provider.GetMetadataObject(metadataName);

            object returnValue = null;

            if (metadata is not null)
            {
                ExportMetadataObject(in metadata);

                returnValue = $"Success: {metadata.Name}";
            }
            
            SetReturnValue(in returnValue);
        }
        private void SetReturnValue(in object value)
        {
            if (!_scope.TrySetValue(_statement.Return.Identifier, value))
            {
                throw new InvalidOperationException($"Error setting return variable {_statement.Return.Identifier}");
            }
        }
        private void GetMetadataObjects(Guid type, in List<DataObject> table)
        {
            foreach (MetadataItem item in _provider.GetMetadataItems(type))
            {
                MetadataObject metadata = _provider.GetMetadataObject(item.Type, item.Uuid);

                DataObject record = metadata.ToDataObject();

                table.Add(record);

                if (metadata is ApplicationObject entity)
                {
                    //if (entity is ITablePartOwner owner)
                    //{
                    //    foreach (TablePart tablePart in owner.TableParts)
                    //    {
                    //        record = new Dictionary<string, object>()
                    //        {
                    //            { "Ссылка",   tablePart.Uuid },
                    //            { "Код",      new decimal(tablePart.TypeCode) },
                    //            { "Тип",      "ТабличнаяЧасть" },
                    //            { "Имя",      tablePart.Name },
                    //            { "Таблица",  tablePart.TableName },
                    //            { "Владелец", entity.Uuid }
                    //        };
                    //        table.Add(record);
                    //    }
                    //}
                }
            }
        }
        private List<DataObject> ReturnMetadataObjects()
        {
            List<DataObject> table = new();

            GetMetadataObjects(MetadataTypes.Catalog, in table);
            GetMetadataObjects(MetadataTypes.Document, in table);
            GetMetadataObjects(MetadataTypes.InformationRegister, in table);
            GetMetadataObjects(MetadataTypes.AccumulationRegister, in table);

            return table;
        }
        private List<DataObject> ReturnPublicationArticles(in IMetadataProvider provider, in string metadataName)
        {
            List<DataObject> articles = new();

            string[] identifiers = metadataName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string identifier = $"{identifiers[0]}.{identifiers[1]}";

            MetadataObject metadata = provider.GetMetadataObject(identifier);

            List<Guid> types = new();
            types.AddRange(MetadataTypes.ReferenceObjectTypes);
            types.AddRange(MetadataTypes.ValueObjectTypes);

            if (metadata is Publication publication)
            {
                foreach (Guid uuid in publication.Articles.Keys)
                {
                    foreach (Guid type in types)
                    {
                        MetadataObject article = provider.GetMetadataObject(type, uuid);

                        if (article is not null)
                        {
                            articles.Add(article.ToDataObject()); break;
                        }
                    }
                }
            }

            return articles;
        }

        private void ExportMetadataObject(in MetadataObject metadata)
        {
            if (metadata is not ApplicationObject entity)
            {
                return;
            }

            FileLogger.Default.Write($"{entity.Name} [{entity.TableName}]");

            foreach (MetadataProperty property in entity.Properties)
            {
                FileLogger.Default.Write($"+ {property.Name} [{property.DbName}]");

                DataTypeDescriptor descriptor = property.PropertyType;

                if (descriptor.IsUuid) { FileLogger.Default.Write($"  - УникальныйИдентификатор"); }
                else if (descriptor.IsBinary) { FileLogger.Default.Write($"  - ДвоичныеДанные"); }
                else if (descriptor.IsValueStorage) { FileLogger.Default.Write($"  - ХранилищеЗначения"); }
                else if (descriptor.IsUnionType(out bool canBeSimple, out bool canBeReference))
                {
                    if (canBeSimple)
                    {
                        if (descriptor.CanBeBoolean) { FileLogger.Default.Write($"  - Булево"); }
                        if (descriptor.CanBeNumeric) { FileLogger.Default.Write($"  - Число"); }
                        if (descriptor.CanBeDateTime) { FileLogger.Default.Write($"  - ДатаВремя"); }
                        if (descriptor.CanBeString) { FileLogger.Default.Write($"  - Строка"); }
                    }

                    if (canBeReference)
                    {
                        List<MetadataItem> items = _provider.ResolveReferencesToMetadataItems(property.References);

                        foreach (MetadataItem item in items)
                        {
                            if (item.Uuid == Guid.Empty) // Общий ссылочный тип
                            {
                                FileLogger.Default.Write($"  - {item.Name}");
                            }
                            else // Конкретный ссылочный тип
                            {
                                FileLogger.Default.Write($"  - Ссылка \"{item.Name}\" [{descriptor.TypeCode}] {{{descriptor.Reference}}}");
                            }
                        }
                    }
                }
                else if (descriptor.CanBeBoolean) { FileLogger.Default.Write($"  - Булево"); }
                else if (descriptor.CanBeNumeric) { FileLogger.Default.Write($"  - Число"); }
                else if (descriptor.CanBeDateTime) { FileLogger.Default.Write($"  - ДатаВремя"); }
                else if (descriptor.CanBeString) { FileLogger.Default.Write($"  - Строка"); }
                else if (descriptor.CanBeReference)
                {
                    MetadataItem item = _provider.GetMetadataItem(descriptor.Reference);
                    FileLogger.Default.Write($"  - Ссылка \"{item.Name}\" [{descriptor.TypeCode}] {{{descriptor.Reference}}}");
                }
            }
        }
    }
}