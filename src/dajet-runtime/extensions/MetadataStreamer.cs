using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;
using DaJet.Scripting.Model;
using TokenType = DaJet.Scripting.TokenType;

namespace DaJet.Runtime
{
    public sealed class MetadataStreamer : UserDefinedProcessor
    {
        private bool _targetIsArray = false;
        private string _target; // output variable
        private string _command; // input variable
        private IDbConnectionFactory _factory;
        private OneDbMetadataProvider _provider;
        public MetadataStreamer(in ScriptScope scope) : base(scope)
        {
            _target = _statement.Return.Identifier;

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_target} is not found");
            }

            declare.Type.Binding = CreateReturnObjectSchema();

            _targetIsArray = (declare.Type.Token == TokenType.Array);
        }
        public override void Process()
        {
            _command = GetCommandName();

            Initialize();

            if (_command == "check-database-schema")
            {
                CheckMetadataAgainstDatabaseSchema();
            }
            else if (_targetIsArray)
            {
                List<DataObject> array = SelectApplicationObjects();
                SetReturnValue(array);
                _next?.Process();
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
                ResolveReferences = true, //FIXME: Из-за этой опции создаём собственного провайдера !
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
        private List<ColumnExpression> CreateReturnObjectSchema()
        {
            return new List<ColumnExpression>()
            {
                new() { Alias = "Тип", Expression = new ScalarExpression() { Token = TokenType.String, Literal = string.Empty } },
                new() { Alias = "Имя", Expression = new ScalarExpression() { Token = TokenType.String, Literal = string.Empty } },
                new() { Alias = "Код", Expression = new ScalarExpression() { Token = TokenType.Number, Literal = "0" } },
                new() { Alias = "Ссылка", Expression = new ScalarExpression() { Token = TokenType.Uuid, Literal = "00000000-0000-0000-0000-000000000000" } },
                new() { Alias = "Синоним", Expression = new ScalarExpression() { Token = TokenType.String, Literal = string.Empty } },
                new() { Alias = "ПолноеИмя", Expression = new ScalarExpression() { Token = TokenType.String, Literal = string.Empty } },
                new() { Alias = "Таблица", Expression = new ScalarExpression() { Token = TokenType.String, Literal = string.Empty } }
            };
        }
        private void SetReturnValue(in object value)
        {
            if (!_scope.TrySetValue(in _target, value))
            {
                throw new InvalidOperationException($"Error setting return variable {_target}");
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
        private List<DataObject> SelectApplicationObjects()
        {
            List<DataObject> array = new();

            foreach (Guid type in MetadataTypes.ApplicationObjectTypes)
            {
                foreach (MetadataItem item in _provider.GetMetadataItems(type))
                {
                    MetadataObject metadata = _provider.GetMetadataObject(item.Type, item.Uuid);

                    DataObject @object = ConvertToDataObject(in metadata);

                    array.Add(@object);
                }
            }

            return array;
        }
        private DataObject ConvertToDataObject(in InfoBase metadata)
        {
            DataObject @object = new();

            @object.SetValue("Тип", "ИнформационнаяБаза");
            @object.SetValue("Ссылка", metadata.Uuid);
            @object.SetValue("Имя", metadata.Name);
            @object.SetValue("Синоним", metadata.Alias);
            @object.SetValue("Комментарий", metadata.Comment);
            @object.SetValue("СмещениеДат", metadata.YearOffset);
            @object.SetValue("ВерсияКонфигурации", metadata.AppConfigVersion);
            @object.SetValue("ВерсияПлатформы", metadata.PlatformVersion);
            @object.SetValue("РежимСовместимости", metadata.CompatibilityVersion);

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
            
            @object.SetValue("Тип", null);

            return @object;
        }
        private DataObject ConvertToDataObject(in ApplicationObject metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Тип", typeName);
            @object.SetValue("Код", metadata.TypeCode);
            @object.SetValue("Ссылка", metadata.Uuid);
            @object.SetValue("Имя", metadata.Name);
            @object.SetValue("Синоним", metadata.Alias);
            @object.SetValue("Таблица", metadata.TableName);
            @object.SetValue("ПолноеИмя", fullName);
            @object.SetValue("Свойства", ConvertToDataObject(metadata.Properties));

            if (metadata is ITablePartOwner owner)
            {
                @object.SetValue("ТабличныеЧасти", ConvertToDataObject(owner.TableParts, in fullName));
            }

            return @object;
        }
        private DataObject ConvertToDataObject(in SharedProperty metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Тип", typeName);
            @object.SetValue("Ссылка", metadata.Uuid);
            @object.SetValue("Имя", metadata.Name);
            @object.SetValue("ПолноеИмя", fullName);
            @object.SetValue("ИмяДанных", metadata.DbName);
            @object.SetValue("ОписаниеТипов", metadata.PropertyType.GetDescriptionRu());
            @object.SetValue("ВнешниеСсылки", ResolveReferences(metadata.References));

            return @object;
        }
        private DataObject ConvertToDataObject(in NamedDataTypeDescriptor metadata)
        {
            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());
            string fullName = $"{typeName}.{metadata.Name}";

            DataObject @object = new();

            @object.SetValue("Тип", typeName);
            @object.SetValue("Ссылка", metadata.Uuid);
            @object.SetValue("Имя", metadata.Name);
            @object.SetValue("ПолноеИмя", fullName);
            @object.SetValue("ОписаниеТипов", metadata.DataTypeDescriptor.GetDescriptionRu());
            @object.SetValue("ВнешниеСсылки", ResolveReferences(metadata.References));

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
                value.SetValue("Имя", item.Name);
                value.SetValue("Ссылка", item.Uuid);
                value.SetValue("Синоним", item.Alias);
                values.Add(value);
            }

            @object.SetValue("Значения", values);

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

        private DataObject ConvertToDataObject(in TablePart table, in string ownerFullName)
        {
            DataObject @object = new();

            @object.SetValue("Тип", "ТабличнаяЧасть");
            @object.SetValue("Код", table.TypeCode);
            @object.SetValue("Ссылка", table.Uuid);
            @object.SetValue("Имя", table.Name);
            @object.SetValue("Синоним", table.Alias);
            @object.SetValue("Таблица", table.TableName);
            @object.SetValue("ПолноеИмя", $"{ownerFullName}.{table.Name}");
            @object.SetValue("Свойства", ConvertToDataObject(table.Properties));

            return @object;
        }
        private DataObject ConvertToDataObject(in MetadataColumn column)
        {
            DataObject @object = new(4);

            @object.SetValue("Имя", column.Name);
            @object.SetValue("ТипДанных", column.TypeName);
            @object.SetValue("Обнуляемый", column.IsNullable);
            @object.SetValue("Назначение", column.Purpose.GetNameRu());

            return @object;
        }
        private DataObject ConvertToDataObject(in MetadataProperty property)
        {
            DataObject @object = new(8);

            @object.SetValue("Имя", property.Name);
            @object.SetValue("Ссылка", property.Uuid);
            @object.SetValue("Синоним", property.Alias);
            @object.SetValue("Назначение", property.Purpose.GetNameRu());
            @object.SetValue("Колонки", ConvertToDataObject(property.Columns));
            @object.SetValue("ОписаниеТипов", property.PropertyType.GetDescriptionRu());
            @object.SetValue("ВнешниеСсылки", ResolveReferences(property.References));

            //List<DataObject> references = ResolveReferences(property.References);
            //CheckReferenceResolvation(in property, in references);

            return @object;
        }
        private List<DataObject> ResolveReferences(in List<Guid> references)
        {
            List<MetadataItem> items = _provider.ResolveReferencesToMetadataItems(references);

            List<DataObject> list = new(items.Count);

            foreach (MetadataItem item in items)
            {
                DataObject @object = new(3);

                @object.SetValue("Ссылка", item.Uuid);

                if (item.Uuid == Guid.Empty) // Общий ссылочный тип
                {
                    @object.SetValue("КодТипа", 0);
                    @object.SetValue("ПолноеИмя", item.Name);
                }
                else // Конкретный ссылочный тип
                {
                    string typeName = MetadataTypes.ResolveNameRu(item.Type);
                    string fullName = $"{typeName}.{item.Name}";

                    if (_provider.TryGetDbName(item.Uuid, out DbName dbn))
                    {
                        @object.SetValue("КодТипа", dbn.Code); // код типа объекта метаданных
                    }
                    else
                    {
                        @object.SetValue("КодТипа", 0);

                        FileLogger.Default.Write($"[ERROR] REFERENCE type code is null [{item.Type}] {{{item.Uuid}}} \"{item.Name}\"");
                    }

                    @object.SetValue("ПолноеИмя", fullName);
                }

                if (@object.GetValue("ПолноеИмя") is string test && string.IsNullOrWhiteSpace(test))
                {
                    FileLogger.Default.Write($"[ERROR] REFERENCE type name is null [{item.Type}] {{{item.Uuid}}} \"{item.Name}\"");
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
        private List<DataObject> ConvertToDataObject(in List<TablePart> tables, in string ownerFullName)
        {
            List<DataObject> list = new(tables.Count);

            foreach (TablePart table in tables)
            {
                list.Add(ConvertToDataObject(in table, in ownerFullName));
            }

            return list;
        }

        //***

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