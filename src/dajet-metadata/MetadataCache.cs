using DaJet.Data;
using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using DaJet.Metadata.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaJet.Metadata
{
    public interface IMetadataCache
    {
        InfoBase InfoBase { get; }
        MetadataItem GetMetadataItem(Guid uuid);
        MetadataItem GetMetadataItem(int typeCode);
        string GetMetadataName(Guid type, Guid uuid);
        IEnumerable<MetadataItem> GetMetadataItems(Guid type);

        MetadataObject GetMetadataObject(Guid uuid);
        MetadataObject GetMetadataObject(int typeCode);
        MetadataObject GetMetadataObject(MetadataItem item);
        MetadataObject GetMetadataObject(string metadataName);
        MetadataObject GetMetadataObject(Guid type, Guid uuid);

        T GetMetadataObject<T>(Guid uuid) where T : MetadataObject;
        T GetMetadataObject<T>(int typeCode) where T : MetadataObject;
        T GetMetadataObject<T>(MetadataItem item) where T : MetadataObject;
        T GetMetadataObject<T>(string metadataName) where T : MetadataObject;
        T GetMetadataObject<T>(Guid type, Guid uuid) where T : MetadataObject;
    }
    public sealed class MetadataCache : IMetadataCache
    {
        private InfoBase _infoBase;
        private readonly string _connectionString;
        private readonly DatabaseProvider _provider;
        private readonly MetadataObjectParserFactory _parsers;

        #region "PRIVATE CACHE VALUES"

        ///<summary>Корневой файл конфигурации из файла "root" таблицы "Config"</summary>
        private Guid _root = Guid.Empty;

        ///<summary>
        ///<b>Кэш объектов метаданных:</b>
        ///<br><b>Ключ 1:</b> UUID общего типа метаданных, например, "Справочник"</br>
        ///<br><b>Ключ 2:</b> UUID объекта метаданных, например, "Справочник.Номенклатура"</br>
        ///<br><b>Значение:</b> кэшируемый объект метаданных</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, Dictionary<Guid, WeakReference<MetadataObject>>> _cache = new();

        ///<summary>
        ///<b>Имена объектов метаданных (первичный индекс для поиска имён):</b>
        ///<br><b>Ключ 1:</b> UUID общего типа метаданных, например, "Справочник"</br>
        ///<br><b>Ключ 2:</b> UUID объекта метаданных, например, "Справочник.Номенклатура"</br>
        ///<br><b>Значение:</b> имя объекта метаданных, например, "Номенклатура"</br>
        ///<br><b>Использование:</b> поиск имени объекта метаданных по его UUID.</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, Dictionary<Guid, string>> _items = new();

        ///<summary>
        ///<b>Имена объектов метаданных (вспомогательный индекс для поиска):</b>
        ///<br><b>Ключ 1:</b> UUID общего типа метаданных, например, "Справочник"</br>
        ///<br><b>Ключ 2:</b> имя объекта метаданных, например, "Номенклатура"</br>
        ///<br><b>Значение:</b> UUID объекта метаданных, например, "Справочник.Номенклатура"</br>
        ///<br><b>Использование:</b> поиск UUID объекта метаданных по его имени.</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, Dictionary<string, Guid>> _names = new();

        ///<summary>
        ///<br><b>Ключ:</b> UUID типа данных "Ссылка" <see cref="ReferenceTypes"/>, например, "ОпределяемыйТип", "ЛюбаяСсылка",</br>
        ///<br>"СправочникСсылка", "СправочникСсылка.Номенклатура"  и т.п. (общие и конкретные типы данных).</br>
        ///<br></br>
        ///<br><b>Значение: </b>Описание ссылочного объекта метаданных,</br>
        ///<br>кроме ссылок на Характеристики <see cref="_characteristics"/>:</br>
        ///<br> - Type = одно из значений <see cref="MetadataTypes"/></br>
        ///<br> - Uuid = <see cref="Guid"/> объекта метаданных</br>
        ///<br> - Name = <see cref="string.Empty"/> (для экономии памяти кэша)</br>
        ///<br></br>
        ///<br><b>Использование:</b> расшифровка <see cref="DataTypeSet"/> при чтении файлов конфигурации.</br>
        ///<br>NOTE: MetadataItem коллекции _references не содержит имени объекта метаданных!</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, MetadataItem> _references = new();

        ///<summary>
        ///<br><b>Ключ:</b> UUID типа данных "Характеристика" - исключительный случай для <see cref="_references"/>,</br>
        ///<br>так как невозможно сопоставить одновременно общий тип, конкретный тип</br>
        ///<br>и тип "Характеристика" (для последнего нет UUID).</br>
        ///<br></br>
        ///<br><b>Значение:</b> UUID объекта метаданных типа "ПланВидовХарактеристик" <see cref="MetadataTypes"/>.</br>
        ///<br></br>
        ///<br><b>Использование:</b> расшифровка <see cref="DataTypeSet"/> при чтении файлов конфигурации.</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, Guid> _characteristics = new();

        ///<summary>
        ///<b>Коллекция подчинённых справочников и их владельцев:</b>
        ///<br><b>Ключ:</b> UUID объекта метаданных <see cref="Catalog"/></br>
        ///<br><b>Значение:</b> список UUID объектов метаданных <see cref="Catalog"/> - владельцев справочника</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, List<Guid>> _owners = new();

        ///<summary>
        ///<b>Коллекция документов и их регистров движений:</b>
        ///<br><b>Ключ:</b> регистр движений <see cref="InformationRegister"/> или <see cref="AccumulationRegister"/></br>
        ///<br><b>Значение:</b> список регистраторов движений <see cref="Document"/></br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, List<Guid>> _registers = new();

        ///<summary>
        ///<br>Кэш идентификаторов <see cref="DbName"/> объектов СУБД</br>
        ///<br>и их сопоставление объектам метаданных конфигурации</br>
        ///</summary>
        private DbNameCache _database;
        internal bool TryGetDbName(Guid uuid, out DbName entry)
        {
            return _database.TryGet(uuid, out entry);
        }
        internal bool TryGetLineNo(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                if (child.Name == MetadataTokens.LineNo)
                {
                    entry = child;

                    return true;
                }
            }

            return false;
        }
        internal bool TryGetChngR(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                if (child.Name.EndsWith(MetadataTokens.ChngR))
                {
                    entry = child;
                    
                    return true;
                }
            }

            return false;
        }

        private void AddName(Guid type, Guid uuid, string name)
        {
            // metadata object uuid to name mapping
            if (_items.TryGetValue(type, out Dictionary<Guid, string> items))
            {
                items.Add(uuid, name);
            }
            else
            {
                _ = _items.TryAdd(type, new Dictionary<Guid, string>()
                {
                    { uuid, name }
                });
            }

            // metadata object name to uuid mapping
            if (_names.TryGetValue(type, out Dictionary<string, Guid> names))
            {
                names.Add(name, uuid);
            }
            else
            {
                _ = _names.TryAdd(type, new Dictionary<string, Guid>()
                {
                    { name, uuid }
                });
            }
        }
        private void AddReference(Guid reference, MetadataItem metadata)
        {
            _ = _references.TryAdd(reference, metadata);
        }
        private void AddCharacteristic(Guid characteristic, Guid metadata)
        {
            _ = _characteristics.TryAdd(characteristic, metadata);
        }
        private void AddCatalogOwner(Guid catalog, Guid owner)
        {
            if (_owners.TryGetValue(catalog, out List<Guid> owners))
            {
                owners.Add(owner);
            }
            else
            {
                _owners.TryAdd(catalog, new List<Guid>() { owner });
            }
        }
        private void AddDocumentRegister(Guid document, Guid register)
        {
            if (_registers.TryGetValue(register, out List<Guid> documents))
            {
                documents.Add(document);
            }
            else
            {
                _registers.TryAdd(register, new List<Guid>() { document });
            }
        }

        internal List<Guid> GetCatalogOwners(Guid catalog)
        {
            if (_owners.TryGetValue(catalog, out List<Guid> owners))
            {
                return owners;
            }

            return null;
        }
        internal List<Guid> GetRegisterRecorders(Guid register)
        {
            if (_registers.TryGetValue(register, out List<Guid> documents))
            {
                return documents;
            }

            return null;
        }

        internal MetadataItem GetCatalogOwner(Guid uuid)
        {
            foreach (Guid type in MetadataTypes.CatalogOwnerTypes)
            {
                if (_items.TryGetValue(type, out Dictionary<Guid, string> items))
                {
                    if (items.TryGetValue(uuid, out string name))
                    {
                        return new MetadataItem(type, uuid, name);
                    }
                }
            }

            return MetadataItem.Empty;
        }
        internal MetadataItem GetRegisterRecorder(Guid uuid)
        {
            if (_items.TryGetValue(MetadataTypes.Document, out Dictionary<Guid, string> items))
            {
                if (items.TryGetValue(uuid, out string name))
                {
                    return new MetadataItem(MetadataTypes.Document, uuid, name);
                }
            }

            return MetadataItem.Empty;
        }

        #endregion

        internal MetadataCache(MetadataCacheOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _provider = options.DatabaseProvider;
            _connectionString = options.ConnectionString;

            _parsers = new MetadataObjectParserFactory(this);
        }
        public InfoBase InfoBase { get { return _infoBase; } }
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseProvider DatabaseProvider { get { return _provider; } }
        public IQueryExecutor CreateQueryExecutor()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return new MsQueryExecutor(_connectionString);
            }
            else if (_provider == DatabaseProvider.PostgreSql)
            {
                return new PgQueryExecutor(_connectionString);
            }

            throw new InvalidOperationException($"Unsupported database provider: {_provider}");
        }

        #region "INITIALIZE CACHE BEFORE USE"

        internal void Initialize()
        {
            InitializeRootFile();
            InitializeDbNameCache();
            InitializeMetadataCache(out InfoBase infoBase);

            _infoBase = infoBase;
        }
        private void InitializeRootFile()
        {
            using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Config, ConfigFiles.Root))
            {
                _root = new RootFileParser().Parse(in reader);
            }
        }
        private void InitializeDbNameCache()
        {
            using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Params, ConfigFiles.DbNames))
            {
                new DbNamesParser().Parse(in reader, out _database);
            }
        }
        private void InitializeMetadataCache(out InfoBase infoBase)
        {
            _cache.Clear();
            _items.Clear();
            _names.Clear();
            _owners.Clear();
            _registers.Clear();
            _references.Clear();
            _characteristics.Clear();

            _references.TryAdd(ReferenceTypes.AnyReference, new MetadataItem(Guid.Empty, Guid.Empty));
            _references.TryAdd(ReferenceTypes.Catalog, new MetadataItem(MetadataTypes.Catalog, Guid.Empty));
            _references.TryAdd(ReferenceTypes.Document, new MetadataItem(MetadataTypes.Document, Guid.Empty));
            _references.TryAdd(ReferenceTypes.Enumeration, new MetadataItem(MetadataTypes.Enumeration, Guid.Empty));
            _references.TryAdd(ReferenceTypes.Publication, new MetadataItem(MetadataTypes.Publication, Guid.Empty));
            _references.TryAdd(ReferenceTypes.Characteristic, new MetadataItem(MetadataTypes.Characteristic, Guid.Empty));

            Dictionary<Guid, List<Guid>> metadata = new()
            {
                //{ MetadataTypes.Constant,             new List<Guid>() }, // Константы
                //{ MetadataTypes.Subsystem,            new List<Guid>() }, // Подсистемы
                { MetadataTypes.NamedDataTypeSet,     new List<Guid>() }, // Определяемые типы
                { MetadataTypes.SharedProperty,       new List<Guid>() }, // Общие реквизиты
                { MetadataTypes.Catalog,              new List<Guid>() }, // Справочники
                { MetadataTypes.Document,             new List<Guid>() }, // Документы
                { MetadataTypes.Enumeration,          new List<Guid>() }, // Перечисления
                { MetadataTypes.Publication,          new List<Guid>() }, // Планы обмена
                { MetadataTypes.Characteristic,       new List<Guid>() }, // Планы видов характеристик
                { MetadataTypes.InformationRegister,  new List<Guid>() }, // Регистры сведений
                { MetadataTypes.AccumulationRegister, new List<Guid>() }  // Регистры накопления
            };

            using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Config, _root))
            {
                new InfoBaseParser().Parse(in reader, out infoBase, in metadata);
            }

            foreach (var entry in metadata)
            {
                Dictionary<Guid, WeakReference<MetadataObject>> items = new();

                if (!_cache.TryAdd(entry.Key, items))
                {
                    continue;
                }

                if (entry.Value.Count == 0)
                {
                    continue;
                }

                foreach (Guid item in entry.Value)
                {
                    _ = items.TryAdd(item, new WeakReference<MetadataObject>(null));
                }
            }

            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            ParallelLoopResult result = Parallel.ForEach(_cache, options, InitializeMetadataItems);
        }
        private void InitializeMetadataItems(KeyValuePair<Guid, Dictionary<Guid, WeakReference<MetadataObject>>> cache)
        {
            Guid type = cache.Key; // общий тип объектов метаданных, например, "Справочник"

            if (!_parsers.TryCreateParser(type, out IMetadataObjectParser parser))
            {
                return; // Unsupported metadata type
            }

            foreach (var entry in cache.Value)
            {
                if (entry.Key == Guid.Empty)
                {
                    continue;
                }

                using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Config, entry.Key))
                {
                    parser.Parse(in reader, out MetadataInfo metadata);

                    if (string.IsNullOrWhiteSpace(metadata.Name))
                    {
                        continue; // accidentally: unsupported metadata object type
                    }

                    if (!string.IsNullOrWhiteSpace(metadata.Name))
                    {
                        AddName(metadata.MetadataType, metadata.MetadataUuid, metadata.Name);
                    }

                    if (metadata.ReferenceUuid != Guid.Empty)
                    {
                        AddReference(metadata.ReferenceUuid, new MetadataItem(metadata.MetadataType, metadata.MetadataUuid));
                    }

                    if (metadata.CharacteristicUuid != Guid.Empty)
                    {
                        AddCharacteristic(metadata.CharacteristicUuid, metadata.MetadataUuid);
                    }

                    if (metadata.CatalogOwners.Count > 0)
                    {
                        foreach (Guid owner in metadata.CatalogOwners)
                        {
                            AddCatalogOwner(metadata.MetadataUuid, owner);
                        }
                    }

                    if (metadata.DocumentRegisters.Count > 0)
                    {
                        foreach (Guid register in metadata.DocumentRegisters)
                        {
                            AddDocumentRegister(metadata.MetadataUuid, register);
                        }
                    }
                }
            }
        }

        #endregion

        #region "INTERNAL METHODS USED BY CONFIGURATOR"

        internal int CountMetadataObjects(Guid type)
        {
            if (!_cache.TryGetValue(type, out Dictionary<Guid, WeakReference<MetadataObject>> entry))
            {
                return 0;
            }

            return entry.Count;
        }
        internal Guid GetSingleMetadataObjectUuid(Guid reference)
        {
            Guid uuid = Guid.Empty;

            if (reference == ReferenceTypes.Catalog)
            {
                uuid = MetadataTypes.Catalog;
            }
            else if (reference == ReferenceTypes.Document)
            {
                uuid = MetadataTypes.Document;
            }
            else if (reference == ReferenceTypes.Enumeration)
            {
                uuid = MetadataTypes.Enumeration;
            }
            else if (reference == ReferenceTypes.Publication)
            {
                uuid = MetadataTypes.Publication;
            }
            else if (reference == ReferenceTypes.Characteristic)
            {
                uuid = MetadataTypes.Characteristic;
            }
            else if (reference == ReferenceTypes.AnyReference)
            {
                /// TODO: очень редкий случай - во всей конфигурации только один ссылочный тип
                /// Single reference type case <see cref="Configurator.ConfigureDataTypeSet"/>
            }
            else
            {
                // Неподдерживаемый общий ссылочный тип, например, "БизнесПроцесс"
                return Guid.Empty;
            }

            if (!_cache.TryGetValue(uuid, out Dictionary<Guid, WeakReference<MetadataObject>> entry))
            {
                return Guid.Empty;
            }

            if (entry.Count == 1)
            {
                foreach (var item in entry)
                {
                    uuid = item.Key;
                }
            }

            return uuid;
        }
        internal bool TryGetReferenceInfo(Guid reference, out MetadataItem info)
        {
            return _references.TryGetValue(reference, out info);
        }
        internal bool TryResolveCharacteristic(Guid reference, out Guid uuid)
        {
            return _characteristics.TryGetValue(reference, out uuid);
        }
        internal IEnumerable<MetadataObject> GetMetadataObjects(Guid type)
        {
            if (!_cache.TryGetValue(type, out Dictionary<Guid, WeakReference<MetadataObject>> entry))
            {
                yield break;
            }

            foreach (KeyValuePair<Guid, WeakReference<MetadataObject>> reference in entry)
            {
                if (!reference.Value.TryGetTarget(out MetadataObject metadata))
                {
                    UpdateMetadataObjectCache(type, reference.Key, out metadata);
                }
                yield return metadata;
            }
        }
        internal MetadataObject GetMetadataObjectCached(Guid type, Guid uuid)
        {
            if (!_cache.TryGetValue(type, out Dictionary<Guid, WeakReference<MetadataObject>> entry))
            {
                return null;
            }

            if (!entry.TryGetValue(uuid, out WeakReference<MetadataObject> reference))
            {
                return null;
            }

            if (!reference.TryGetTarget(out MetadataObject metadata))
            {
                UpdateMetadataObjectCache(type, uuid, out metadata);
            }

            return metadata;
        }        
        private MetadataObject GetMetadataObjectCached(in string typeName, in string objectName, in string tableName)
        {
            Guid type = MetadataTypes.ResolveName(typeName);

            if (type == Guid.Empty)
            {
                return null;
            }

            if (!_names.TryGetValue(type, out Dictionary<string, Guid> names))
            {
                return null;
            }

            if (!names.TryGetValue(objectName, out Guid uuid))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                return GetMetadataObjectCached(type, uuid);
            }

            MetadataObject metadata = GetMetadataObjectCached(type, uuid);

            if (metadata is not ITablePartOwner owner)
            {
                return null;
            }

            foreach (TablePart table in owner.TableParts)
            {
                if (table.Name == tableName)
                {
                    return table;
                }
            }

            return null;
        }
        private void UpdateMetadataObjectCache(Guid type, Guid uuid, out MetadataObject metadata)
        {
            if (!_cache.TryGetValue(type, out Dictionary<Guid, WeakReference<MetadataObject>> entry))
            {
                throw new InvalidOperationException(); // this should not happen
            }

            if (!entry.TryGetValue(uuid, out WeakReference<MetadataObject> reference))
            {
                throw new InvalidOperationException(); // this should not happen
            }

            GetMetadataObject(type, uuid, out metadata);

            reference.SetTarget(metadata);
        }
        private void GetMetadataObject(Guid type, Guid uuid, out MetadataObject metadata)
        {
            if (!_parsers.TryCreateParser(type, out IMetadataObjectParser parser))
            {
                throw new InvalidOperationException($"Unsupported metadata type {{{type}}}");
            }
            else if (parser == null)
            {
                string metadataType = MetadataTypes.ResolveName(type);
                throw new InvalidOperationException($"Metadata type parser is under development \"{metadataType}\"");
            }

            if (type == MetadataTypes.SharedProperty || type == MetadataTypes.NamedDataTypeSet)
            {
                using (ConfigFileReader reader = new(_provider, _connectionString, ConfigTables.Config, uuid))
                {
                    parser.Parse(in reader, out metadata);
                }

                if (type == MetadataTypes.SharedProperty)
                {
                    Configurator.ConfigureDatabaseNames(this, in metadata);
                }
            }
            else
            {
                GetApplicationObject(uuid, in parser, out metadata);
            }
        }
        private void GetApplicationObject(Guid uuid, in IMetadataObjectParser parser, out MetadataObject metadata)
        {
            using (ConfigFileReader reader = new(_provider, _connectionString, ConfigTables.Config, uuid))
            {
                parser.Parse(in reader, out metadata);
            }

            // Конфигурирование DBNames в том числе устанавливает
            // числовой код типа (type code) объекта метаданных.
            // Важно!
            // Этот код типа используется для дальнейшего конфигурирования
            // основных реквизитов (system properties) объектов метаданных в тех случаях,
            // когда соответствующие свойства имеют одиночный (single) ссылочный тип данных:
            // - Справочник.Родитель;
            // - Справочник.Владелец;
            // - Регистр.Регистратор.
            Configurator.ConfigureDatabaseNames(this, in metadata);

            // Shared properties are always in the bottom.
            // They have default property purpose - Property.
            Configurator.ConfigureSharedProperties(this, in metadata);

            Configurator.ConfigureSystemProperties(this, in metadata);

            if (metadata is ApplicationObject owner && metadata is ITablePartOwner)
            {
                // ConfigureDatabaseNames should be called for the owner first:
                // the name of table part reference field is dependent on table name of the owner
                // Owner table name: _Reference1008
                // Table part reference field name: _Reference1008_IDRRef
                Configurator.ConfigureTableParts(this, in owner);
            }

            if (metadata is Publication publication)
            {
                Configurator.ConfigureArticles(this, in publication);
            }

            if (metadata is IPredefinedValueOwner)
            {
                Configurator.ConfigurePredefinedValues(this, in metadata);
            }
        }

        #endregion

        #region "RESOLVE DATA TYPE SET REFERENCES"

        /// <summary>
        /// Функция сопоставления ссылочных типов данных объекта "ОписаниеТипов" объектам метаданных.
        /// <br>Список ссылочных типов объекта <see cref="DataTypeSet"/> получает парсер <see cref="DataTypeSetParser"/>.</br>
        /// </summary>
        /// <param name="references">Список ссылочных типов данных объекта "ОписаниеТипов".</param>
        /// <returns></returns>
        internal List<MetadataItem> ResolveReferences(in List<Guid> references)
        {
            List<MetadataItem> metadata = new();

            for (int i = 0; i < references.Count; i++)
            {
                Guid reference = references[i];

                if (reference == Guid.Empty) { continue; }

                MetadataItem item = ResolveReferenceType(reference);

                if (item != MetadataItem.Empty)
                {
                    metadata.Add(item);
                }
            }

            return metadata;
        }
        internal MetadataItem ResolveReferenceType(Guid reference)
        {
            // RULES (правила разрешения ссылочных типов данных для объекта "ОписаниеТипов"):
            // 1. DataTypeSet (описание типа данных свойства объекта) может ссылаться только на
            //    один экземпляр определяемого типа или плана видов характеристик (характеристику).
            //    В таком случае указание дополнительных типов данных для данного свойства невозможно.
            // 2. Определяемый тип или характеристика не могут ссылаться на другие определяемые типы или характеристики.
            // 3. Если ссылочный тип имеет значение, например, "СправочникСсылка", то есть любой справочник,
            //    то в таком случае необходимо вычислить количество справочников в составе конфигурации:
            //    если возможным справочником будет только один, то это будет single reference type.
            // 4. То же самое, что и для пункта #3, касается значения типа "ЛюбаяСсылка":
            //    если в составе конфигурации имеется только один ссылочный тип данных, например,
            //    только один справочник или документ, то это будет single reference type.
            // 5. К специальным ссылочным типам данных относятся "УникальныйИдентификатор" и "ХранилищеЗначения".
            //    Согласно алгоритму парсера DataTypeSetParser, они в качестве значения параметра reference отсутствуют.

            if (reference == SingleTypes.ValueStorage)
            {
                return new MetadataItem(SingleTypes.ValueStorage, Guid.Empty, "ХранилищеЗначения");
            }
            else if (reference == SingleTypes.Uniqueidentifier)
            {
                return new MetadataItem(SingleTypes.Uniqueidentifier, Guid.Empty, "УникальныйИдентификатор");
            }
            else if (reference == ReferenceTypes.AnyReference)
            {
                return new MetadataItem(ReferenceTypes.AnyReference, Guid.Empty, "ЛюбаяСсылка");
            }
            else if (reference == ReferenceTypes.Catalog)
            {
                return new MetadataItem(ReferenceTypes.Catalog, Guid.Empty, "СправочникСсылка");
            }
            else if (reference == ReferenceTypes.Document)
            {
                return new MetadataItem(ReferenceTypes.Document, Guid.Empty, "ДокументСсылка");
            }
            else if (reference == ReferenceTypes.Enumeration)
            {
                return new MetadataItem(ReferenceTypes.Enumeration, Guid.Empty, "ПеречислениеСсылка");
            }
            else if (reference == ReferenceTypes.Publication)
            {
                return new MetadataItem(ReferenceTypes.Publication, Guid.Empty, "ПланОбменаСсылка");
            }
            else if (reference == ReferenceTypes.Characteristic)
            {
                return new MetadataItem(ReferenceTypes.Characteristic, Guid.Empty, "ПланВидовХарактеристикСсылка");
            }
            else if (_characteristics.TryGetValue(reference, out Guid uuid))
            {
                string name = GetMetadataName(MetadataTypes.Characteristic, uuid);

                ///NOTE: Небольшой хак ¯\_(ツ)_/¯ <see cref="MetadataItem.ToString()"/>
                return new MetadataItem(ReferenceTypes.Characteristic, uuid, name); // Характеристика
                // Но не ... return new MetadataItem(MetadataTypes.Characteristic, uuid, name); // ПланВидовХарактеристикСсылка
            }
            else if (_references.TryGetValue(reference, out MetadataItem info))
            {
                //NOTE: MetadataItem коллекции _references не содержит имени объекта метаданных !
                string name = GetMetadataName(info.Type, info.Uuid);
                
                return new MetadataItem(info.Type, info.Uuid, name);
            }

            return new MetadataItem(Guid.Empty, reference); // Неподдерживаемый общий или конкретный ссылочный тип
        }

        #endregion

        #region "GETTING METADATA OBJECT/S INTERFACE IMPLEMENTATION"

        private string[] GetIdentifiers(string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string[] identifiers = metadataName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (identifiers.Length < 2)
            {
                throw new FormatException(nameof(metadataName));
            }

            //string typeName = identifiers[0];
            //string objectName = identifiers[1];
            //
            //string tablePartName = null;
            //if (names.Length == 3)
            //{
            //    tablePartName = names[2];
            //}

            return identifiers;
        }

        public string GetMetadataName(Guid type, Guid uuid)
        {
            if (_items.TryGetValue(type, out Dictionary<Guid, string> items))
            {
                if (items.TryGetValue(uuid, out string name))
                {
                    return name;
                }
            }

            return string.Empty;
        }
        public MetadataItem GetMetadataItem(Guid uuid)
        {
            foreach (Guid type in MetadataTypes.ReferenceObjectTypes)
            {
                if (_items.TryGetValue(type, out Dictionary<Guid, string> items))
                {
                    if (items.TryGetValue(uuid, out string name))
                    {
                        return new MetadataItem(type, uuid, name);
                    }
                }
            }

            return MetadataItem.Empty;
        }
        public MetadataItem GetMetadataItem(int typeCode)
        {
            if (!_database.TryGet(typeCode, out DbName dbn))
            {
                return MetadataItem.Empty;
            }

            Guid type;

            if (dbn.Name == MetadataTokens.Reference)
            {
                type = MetadataTypes.Catalog;
            }
            else if (dbn.Name == MetadataTokens.Document)
            {
                type = MetadataTypes.Document;
            }
            else if (dbn.Name == MetadataTokens.Enum)
            {
                type = MetadataTypes.Enumeration;
            }
            else if (dbn.Name == MetadataTokens.Chrc)
            {
                type = MetadataTypes.Characteristic;
            }
            else if (dbn.Name == MetadataTokens.Node)
            {
                type = MetadataTypes.Publication;
            }
            else
            {
                return MetadataItem.Empty;
            }

            if (!_items.TryGetValue(type, out Dictionary<Guid, string> items))
            {
                return MetadataItem.Empty;
            }

            if (!items.TryGetValue(dbn.Uuid, out string name))
            {
                return MetadataItem.Empty;
            }

            return new MetadataItem(type, dbn.Uuid, name);
        }
        public IEnumerable<MetadataItem> GetMetadataItems(Guid type)
        {
            if (!_items.TryGetValue(type, out Dictionary<Guid, string> items))
            {
                yield break;
            }

            foreach (var item in items)
            {
                yield return new MetadataItem(type, item.Key, item.Value);
            }
        }

        public MetadataObject GetMetadataObject(Guid uuid)
        {
            MetadataItem item = GetMetadataItem(uuid);

            if (item == MetadataItem.Empty)
            {
                return null;
            }

            return GetMetadataObject(item);
        }
        public MetadataObject GetMetadataObject(int typeCode)
        {
            MetadataItem item = GetMetadataItem(typeCode);

            if (item == MetadataItem.Empty)
            {
                return null;
            }

            return GetMetadataObject(item);
        }
        public MetadataObject GetMetadataObject(MetadataItem item)
        {
            return GetMetadataObjectCached(item.Type, item.Uuid);
        }
        public MetadataObject GetMetadataObject(string metadataName)
        {
            string[] identifiers = GetIdentifiers(metadataName);

            if (identifiers.Length > 2)
            {
                return GetMetadataObjectCached(identifiers[0], identifiers[1], identifiers[2]);
            }
            else
            {
                return GetMetadataObjectCached(identifiers[0], identifiers[1], null);
            }
        }
        public MetadataObject GetMetadataObject(Guid type, Guid uuid)
        {
            return GetMetadataObjectCached(type, uuid);
        }

        public T GetMetadataObject<T>(Guid uuid) where T : MetadataObject
        {
            return GetMetadataObject(uuid) as T;
        }
        public T GetMetadataObject<T>(int typeCode) where T : MetadataObject
        {
            return GetMetadataObject(typeCode) as T;
        }
        public T GetMetadataObject<T>(MetadataItem item) where T : MetadataObject
        {
            return GetMetadataObject(item) as T;
        }
        public T GetMetadataObject<T>(string metadataName) where T : MetadataObject
        {
            return GetMetadataObject(metadataName) as T;
        }
        public T GetMetadataObject<T>(Guid type, Guid uuid) where T : MetadataObject
        {
            return GetMetadataObject(type, uuid) as T;
        }

        #endregion

        #region "PUBLICATION AND CHANGE TABLE UTILITY METHODS"

        internal string GetMainTableName(Guid uuid)
        {
            if (!TryGetDbName(uuid, out DbName entry))
            {
                return string.Empty;
            }

            if (_provider == DatabaseProvider.PostgreSql)
            {
                return $"_{entry.Name}{entry.Code}".ToLowerInvariant();
            }

            return $"_{entry.Name}{entry.Code}";
        }
        internal string GetChangeTableName(Guid uuid)
        {
            if (!TryGetChngR(uuid, out DbName entry))
            {
                return string.Empty;
            }

            if (_provider == DatabaseProvider.PostgreSql)
            {
                return $"_{entry.Name}{entry.Code}".ToLowerInvariant();
            }

            return $"_{entry.Name}{entry.Code}";
        }
        public Publication GetPublication(string name)
        {
            if (!_names.TryGetValue(MetadataTypes.Publication, out Dictionary<string, Guid> names))
            {
                return null;
            }

            if (!names.TryGetValue(name, out Guid uuid))
            {
                return null;
            }

            Publication publication = GetMetadataObject<Publication>(MetadataTypes.Publication, uuid);

            if (publication == null)
            {
                return null;
            }

            PublicationDataMapper mapper = new(this);

            mapper.Select(in publication);

            return publication;
        }
        public EntityChangeTable GetEntityChangeTable(ApplicationObject entity)
        {
            // TODO: Поддерживаются только ссылочные типы данных
            // TODO: Добавить поддержку для регистров (составные ключи)

            if (!TryGetChngR(entity.Uuid, out _))
            {
                return null;
            }

            EntityChangeTable table = new(entity);

            Configurator.ConfigureSystemProperties(this, table);

            return table;
        }

        #endregion
    }
}