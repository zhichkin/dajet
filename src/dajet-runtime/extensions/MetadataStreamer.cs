using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;

namespace DaJet.Runtime
{
    public sealed class MetadataStreamer : UserDefinedProcessor
    {
        private string _command;
        private IDbConnectionFactory _factory;
        private OneDbMetadataProvider _provider;
        public MetadataStreamer(in ScriptScope scope) : base(scope) { }
        public override void Process()
        {
            _command = GetCommandName();

            Initialize();

            if (_command == "check-database-schema")
            {
                CheckMetadataAgainstDatabaseSchema();
            }
            else
            {
                foreach (DataObject metadata in MetadataStream())
                {
                    SetReturnValue(metadata);

                    _next?.Process();
                }
            }

            SetReturnValue(null);
        }
        private string GetCommandName()
        {
            _ = _scope.TryGetValue(_statement.Variables[0].Identifier, out object value);

            if (value is not string command)
            {
                throw new ArgumentException($"[{nameof(MetadataStreamer)}] command name is missing");
            }

            return command;
        }
        private void Initialize()
        {
            if (_provider is not null) { return; }

            Uri uri = _scope.GetDatabaseUri(); //_scope.GetUri(source);

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
                throw new NotSupportedException($"[{nameof(MetadataStreamer)}] database {uri.Scheme} is not supported");
            }

            if (!OneDbMetadataProvider.TryCreateMetadataProvider(in options, out _provider, out string error))
            {
                FileLogger.Default.Write(error);
            }
        }
        private void SetReturnValue(in object value)
        {
            if (!_scope.TrySetValue(_statement.Return.Identifier, value))
            {
                throw new InvalidOperationException($"Error setting return variable {_statement.Return.Identifier}");
            }
        }
        private IEnumerable<DataObject> MetadataStream()
        {
            DataObject @object = ConvertToDataObject(_provider.InfoBase);

            yield return @object;

            foreach (Guid type in MetadataTypes.AllSupportedTypes)
            {
                foreach (MetadataItem item in _provider.GetMetadataItems(type))
                {
                    MetadataObject metadata = _provider.GetMetadataObject(item.Type, item.Uuid);

                    @object = ConvertToDataObject(in metadata);

                    yield return @object;
                }
            }
        }
        private DataObject ConvertToDataObject(in InfoBase metadata)
        {
            DataObject @object = new();

            @object.SetValue("Type", "ИнформационнаяБаза");
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("Alias", metadata.Alias);
            @object.SetValue("Comment", metadata.Comment);
            @object.SetValue("YearOffset", metadata.YearOffset);
            @object.SetValue("PlatformVersion", metadata.PlatformVersion);
            @object.SetValue("CompatibilityVersion", metadata.CompatibilityVersion);

            return @object;
        }
        private DataObject ConvertToDataObject(in MetadataObject metadata)
        {
            if (metadata is Account account) { return ConvertToDataObject(in account); }
            else if (metadata is Catalog catalog) { return ConvertToDataObject(in catalog); }
            else if (metadata is Document document) { return ConvertToDataObject(in document); }
            else if (metadata is Enumeration enumeration) { return ConvertToDataObject(in enumeration); }
            else if (metadata is Publication publication) { return ConvertToDataObject(in publication); }
            else if (metadata is Characteristic characteristic) { return ConvertToDataObject(in characteristic); }
            else if (metadata is SharedProperty property) { return ConvertToDataObject(in property); }
            else if (metadata is AccountingRegister register1) { return ConvertToDataObject(in register1); }
            else if (metadata is InformationRegister register2) { return ConvertToDataObject(in register2); }
            else if (metadata is AccumulationRegister register3) { return ConvertToDataObject(in register3); }
            else if (metadata is NamedDataTypeDescriptor descriptor) { return ConvertToDataObject(in descriptor); }

            DataObject @object = new(1);
            
            @object.SetValue("Type", "UNKNOWN");

            FileLogger.Default.Write($"[UNKNOWN] \"{metadata.Name}\" {{{metadata.Uuid}}}");

            return @object;
        }
        private DataObject ConvertToDataObject(in ApplicationObject metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Code", metadata.TypeCode);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("Alias", metadata.Alias);
            @object.SetValue("DbName", metadata.TableName);
            @object.SetValue("FullName", fullName);
            @object.SetValue("Properties", ConvertToDataObject(metadata.Properties));

            if (metadata is ITablePartOwner owner)
            {
                @object.SetValue("TableParts", ConvertToDataObject(owner.TableParts, in fullName));
            }

            return @object;
        }
        private DataObject ConvertToDataObject(in SharedProperty metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("FullName", fullName);
            @object.SetValue("DbName", metadata.DbName);
            @object.SetValue("Descriptor", metadata.PropertyType.GetDescriptionRu());
            @object.SetValue("References", ResolveReferences(metadata.References));

            return @object;
        }
        private DataObject ConvertToDataObject(in NamedDataTypeDescriptor metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Type", typeName);
            @object.SetValue("Uuid", metadata.Uuid);
            @object.SetValue("Name", metadata.Name);
            @object.SetValue("FullName", fullName);
            @object.SetValue("Descriptor", metadata.DataTypeDescriptor.GetDescriptionRu());
            @object.SetValue("References", ResolveReferences(metadata.References));

            return @object;
        }
        private DataObject ConvertToDataObject(in Account metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in Catalog metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in Document metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in Enumeration metadata)
        {
            DataObject @object = ConvertToDataObject(metadata as ApplicationObject);

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
        private DataObject ConvertToDataObject(in Publication metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in Characteristic metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in AccountingRegister metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in InformationRegister metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }
        private DataObject ConvertToDataObject(in AccumulationRegister metadata)
        {
            return ConvertToDataObject(metadata as ApplicationObject);
        }

        private void CheckReferenceResolvation(in MetadataProperty property, in List<DataObject> references)
        {
            DataTypeDescriptor descriptor = property.PropertyType;

            if (descriptor.CanBeReference)
            {
                MetadataItem test;

                // Проверка разрешимости одиночной ссылки

                if (descriptor.TypeCode == 0 && descriptor.Reference != Guid.Empty)
                {
                    FileLogger.Default.Write($"[WARNING] \"{property.Name}\" {{{property.Uuid}}} TypeCode is null when Reference is not (unsupported metadata type)");
                }
                else if (descriptor.TypeCode != 0 && descriptor.Reference == Guid.Empty)
                {
                    FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} Reference is null when TypeCode is not");
                }
                else if (descriptor.TypeCode != 0 && descriptor.Reference != Guid.Empty)
                {
                    MetadataItem testTypeCode = _provider.GetMetadataItem(descriptor.TypeCode);

                    if (testTypeCode == MetadataItem.Empty)
                    {
                        FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} TypeCode not resolved");
                    }

                    MetadataItem testReference = _provider.GetMetadataItem(descriptor.Reference);

                    if (testReference == MetadataItem.Empty)
                    {
                        FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} Reference not resolved [{property.PropertyType.GetDescriptionRu()}]");
                    }

                    if (testTypeCode != testReference)
                    {
                        FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} TypeCode and Reference resolved differently [{property.PropertyType.GetDescriptionRu()}]");
                    }
                }
                else if (descriptor.TypeCode == 0 && descriptor.Reference == Guid.Empty)
                {
                    if (property.References.Count == 0)
                    {
                        //FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} References is empty");
                    }
                }

                // Проверка наличия и разрешимости логических ссылок на соответствующие объекты метаданных

                if (property.References.Count == 0)
                {
                    if (property.Purpose == PropertyPurpose.System && property.Name == "Ссылка")
                    {
                        // Исключение из правил - это первичный ключ, а не внешний
                    }
                    else
                    {
                        FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} References missing");
                    }
                }
                else
                {
                    foreach (DataObject reference in references)
                    {
                        if (reference.TryGetValue("Uuid", out object value) && value is Guid uuid)
                        {
                            if (uuid == Guid.Empty)
                            {
                                if (reference.TryGetValue("Type", out object type) && type is string fullName)
                                {
                                    //FileLogger.Default.Write($"[WARNING] \"{property.Name}\" {{{property.Uuid}}} [{fullName}]");
                                }
                                else
                                {
                                    FileLogger.Default.Write($"[FATAL ERROR] Reference.Type is not initialized");
                                }
                            }
                            else
                            {
                                test = _provider.GetMetadataItem(uuid);

                                if (test == MetadataItem.Empty)
                                {
                                    FileLogger.Default.Write($"[ERROR] \"{property.Name}\" {{{property.Uuid}}} failed to resolve reference");
                                }
                            }
                        }
                        else
                        {
                            FileLogger.Default.Write($"[FATAL ERROR] Reference.Uuid is not initialized");
                        }
                    }
                }
            }
        }
        private DataObject ConvertToDataObject(in MetadataColumn column)
        {
            DataObject @object = new(4);

            @object.SetValue("Name", column.Name);
            @object.SetValue("Type", column.TypeName);
            @object.SetValue("IsNullable", column.IsNullable);
            @object.SetValue("Purpose", column.Purpose.GetNameEn());

            return @object;
        }
        private DataObject ConvertToDataObject(in MetadataProperty property)
        {
            DataObject @object = new(8);
            
            @object.SetValue("Type", property.Purpose.GetNameRu());
            @object.SetValue("Uuid", property.Uuid);
            @object.SetValue("Name", property.Name);
            @object.SetValue("Alias", property.Alias);
            @object.SetValue("DbName", property.DbName);
            @object.SetValue("Columns", ConvertToDataObject(property.Columns));
            @object.SetValue("Descriptor", property.PropertyType.GetDescriptionRu());
            List<DataObject> references = ResolveReferences(property.References);
            @object.SetValue("References", references);

            CheckReferenceResolvation(in property, in references);

            return @object;
        }
        private List<DataObject> ResolveReferences(in List<Guid> references)
        {
            List<MetadataItem> items = _provider.ResolveReferencesToMetadataItems(references);

            List<DataObject> list = new(items.Count);

            foreach (MetadataItem item in items)
            {
                DataObject @object = new(3);

                @object.SetValue("Uuid", item.Uuid);

                if (item.Uuid == Guid.Empty) // Общий ссылочный тип
                {
                    @object.SetValue("Code", 0);
                    @object.SetValue("Type", item.Name);
                }
                else // Конкретный ссылочный тип
                {
                    string typeName = MetadataTypes.ResolveNameRu(item.Type);
                    string fullName = $"{typeName}.{item.Name}";

                    if (_provider.TryGetDbName(item.Uuid, out DbName dbn))
                    {
                        @object.SetValue("Code", dbn.Code); // код типа объекта метаданных
                    }
                    else
                    {
                        @object.SetValue("Code", 0);

                        FileLogger.Default.Write($"[ERROR] REFERENCE TypeCode is null [{item.Type}] {{{item.Uuid}}} \"{item.Name}\"");
                    }

                    @object.SetValue("Type", fullName);
                }

                if (@object.GetValue("Type") is string test && string.IsNullOrWhiteSpace(test))
                {
                    FileLogger.Default.Write($"[ERROR] REFERENCE TypeName is null [{item.Type}] {{{item.Uuid}}} \"{item.Name}\"");
                }

                list.Add(@object);
            }

            return list;
        }
        private List<DataObject> ConvertToDataObject(in List<MetadataColumn> columns)
        {
            List<DataObject> list = new(columns.Count);

            foreach (MetadataColumn column in columns)
            {
                list.Add(ConvertToDataObject(in column));
            }

            return list;
        }
        private List<DataObject> ConvertToDataObject(in List<MetadataProperty> properties)
        {
            List<DataObject> list = new(properties.Count);

            foreach (MetadataProperty property in properties)
            {
                list.Add(ConvertToDataObject(in property));
            }

            return list;
        }
        private DataObject ConvertToDataObject(in TablePart table, in string ownerFullName)
        {
            DataObject @object = new();

            @object.SetValue("Type", "ТабличнаяЧасть");
            @object.SetValue("Code", table.TypeCode);
            @object.SetValue("Uuid", table.Uuid);
            @object.SetValue("Name", table.Name);
            @object.SetValue("Alias", table.Alias);
            @object.SetValue("DbName", table.TableName);
            @object.SetValue("FullName", $"{ownerFullName}.{table.Name}");
            @object.SetValue("Properties", ConvertToDataObject(table.Properties));

            return @object;
        }
        private List<DataObject> ConvertToDataObject(in List<TablePart> tables, in string ownerFullName)
        {
            List<DataObject> list = new(tables.Count);

            foreach (TablePart table in tables)
            {
                list.Add(ConvertToDataObject(in table, in ownerFullName));
            }

            return list;
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

        //*********

        private void CheckMetadataAgainstDatabaseSchema()
        {
            foreach (Guid type in MetadataTypes.ApplicationObjectTypes)
            {
                foreach (MetadataItem item in _provider.GetMetadataItems(type))
                {
                    MetadataObject metadata = _provider.GetMetadataObject(item.Type, item.Uuid);

                    if (metadata is ApplicationObject entity)
                    {
                        string entityName = entity.Name;
                        PerformDatabaseSchemaCheck(in entityName, entity.TableName, entity.Properties);

                        if (entity is ITablePartOwner owner)
                        {
                            foreach (TablePart table in owner.TableParts)
                            {
                                string tableName = $"{entityName}.{table.Name}";
                                PerformDatabaseSchemaCheck(in tableName, table.TableName, table.Properties);
                            }
                        }
                    }
                }
            }
        }
        private void PerformDatabaseSchemaCheck(in string entityName, in string tableName, in List<MetadataProperty> properties)
        {
            SqlMetadataReader sql = new();
            sql.UseDatabaseProvider(_provider.DatabaseProvider);
            sql.UseConnectionString(_provider.ConnectionString);
            
            MetadataCompareAndMergeService comparator = new();

            List<DaJet.Metadata.Services.SqlFieldInfo> fields = sql.GetSqlFieldsOrderedByName(tableName);

            List<string> source = comparator.PrepareComparison(fields); // эталон (как должно быть)
            List<string> target = comparator.PrepareComparison(properties); // испытуемый на соответствие эталону

            comparator.Compare(target, source, out List<string> delete_list, out List<string> insert_list);

            if (delete_list.Count == 0 && insert_list.Count == 0)
            {
                return; // success - проверка прошла успешно
            }

            FileLogger.Default.Write($"[{tableName}] {entityName}");

            if (delete_list.Count > 0)
            {
                FileLogger.Default.Write($"* delete (лишние поля)");

                foreach (string field in delete_list)
                {
                    FileLogger.Default.Write($"  - {field}");
                }
            }

            if (insert_list.Count > 0)
            {
                FileLogger.Default.Write($"* insert (отсутствующие поля)");

                foreach (string field in insert_list)
                {
                    FileLogger.Default.Write($"  - {field}");
                }
            }
        }
    }
}