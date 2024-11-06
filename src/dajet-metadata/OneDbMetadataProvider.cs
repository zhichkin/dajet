using DaJet.Data;
using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Extensions;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using DaJet.Metadata.Services;
using Microsoft.Data.SqlClient;
using Npgsql;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DaJet.Metadata
{
    public sealed class OneDbMetadataProvider : IMetadataProvider
    {
        #region "CONSTANTS"

        ///_ConfigVersion is the root file identifier
        ///<see cref="ExtensionInfo.RootFile"/> and
        ///<see cref="DecodeZippedInfo"/> method below
        private const string MS_SELECT_EXTENSIONS_OLD =
            "SELECT _IDRRef, _ConfigVersion, _ExtensionOrder, _ExtName, _UpdateTime, " +
            "_ExtSynonym, _ExtVersion, _SafeMode, _SecurityProfileName, _Version " +
            "FROM _ExtensionsInfo ORDER BY _ExtensionOrder;";

        private const string PG_SELECT_EXTENSIONS_OLD =
            "SELECT _idrref, _ConfigVersion, _extensionorder, CAST(_extname AS varchar), _updatetime, " +
            "CAST(_extsynonym AS varchar), CAST(_extversion AS varchar), _safemode, CAST(_securityprofilename AS varchar), _version " +
            "FROM _extensionsinfo ORDER BY _extensionorder;";

        private const string IS_NEW_AGE_EXTENSIONS_SUPPORTED =
            "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '_extensionsinfo' AND COLUMN_NAME = '_extensionzippedinfo';";

        private const string MS_SELECT_EXTENSIONS_NEW =
            "SELECT _IDRRef, _ExtensionOrder, _ExtName, _UpdateTime, " +
            "_ExtensionUsePurpose, _ExtensionScope, _ExtensionZippedInfo, " +
            "_MasterNode, _UsedInDistributedInfoBase, _Version " +
            "FROM _ExtensionsInfo ORDER BY " +
            "CASE WHEN SUBSTRING(_MasterNode, CAST(1.0 AS INT), CAST(34.0 AS INT)) = N'0:00000000000000000000000000000000' " +
            "THEN 0x01 ELSE 0x00 END, _ExtensionUsePurpose, _ExtensionScope, _ExtensionOrder;";

        private const string PG_SELECT_EXTENSIONS_NEW =
            "SELECT _idrref, _extensionorder, CAST(_extname AS varchar), _updatetime, " +
            "_extensionusepurpose, _extensionscope, _extensionzippedinfo, " +
            "CAST(_masternode AS varchar), _usedindistributedinfobase, _version " +
            "FROM _extensionsinfo ORDER BY " +
            "CASE WHEN SUBSTRING(CAST(_masternode AS varchar), 1, 34) = '0:00000000000000000000000000000000' " +
            "THEN 1 ELSE 0 END, _extensionusepurpose, _extensionscope, _extensionorder;";

        #endregion

        private InfoBase _infoBase;
        private readonly ExtensionInfo _extension;
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
        ///<b>Заимствованные и собственные объекты метаданных расширений:</b>
        ///<br><b>Ключ:</b> UUID объекта метаданных, например, "Справочник.Номенклатура"</br>
        ///<br><b>Значение:</b> информация для загрузки метаданных объекта из расширения</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, MetadataItemEx> _extended = new();

        ///<summary>
        ///<b>Кэш расширений основной конфигурации:</b>
        ///<br><b>Ключ:</b> идентификатор расширения _IDRRef в таблице _ExtensionsInfo</br>
        ///<br><b>Значение:</b> кэш объектов метаданных расширения</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, OneDbMetadataProvider> _extensions = new();

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
        ///<br><b>Использование:</b> расшифровка <see cref="DataTypeDescriptor"/> при чтении файлов конфигурации.</br>
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
        ///<br><b>Использование:</b> расшифровка <see cref="DataTypeDescriptor"/> при чтении файлов конфигурации.</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, Guid> _characteristics = new();

        ///<summary>
        ///<br><b>Ключ:</b> UUID типа данных "Характеристика"</br>
        ///<br><b>Значение:</b> Типы значений характеристики</br>
        ///<br><b>Использование:</b> расшифровка <see cref="DataTypeDescriptor"/> при чтении файлов конфигурации.</br>
        ///</summary>
        private readonly ConcurrentDictionary<Guid, DataTypeDescriptor> _typeDescriptors = new();

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
        ///<br>Кэш идентификаторов <see cref="DbName"/> объектов СУБД,</br>
        ///<br>а также числовые коды ссылочных типов данных (!)</br>
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
        internal bool TryGetAccumRgT(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                // Таблица итогов регистра накопления (остатков или оборотов)
                if (child.Name == MetadataTokens.AccumRgT ||
                    child.Name == MetadataTokens.AccumRgTn)
                {
                    entry = child;

                    return true;
                }
            }

            return false;
        }
        internal bool TryGetAccumRgOpt(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                if (child.Name == MetadataTokens.AccumRgOpt)
                {
                    entry = child;

                    return true;
                }
            }

            return false;
        }
        internal bool TryGetInfoRgOpt(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                if (child.Name == MetadataTokens.InfoRgOpt)
                {
                    entry = child;

                    return true;
                }
            }

            return false;
        }
        internal bool TryGetInfoRgSF(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                if (child.Name == MetadataTokens.InfoRgSF)
                {
                    entry = child;

                    return true;
                }
            }

            return false;
        }
        internal bool TryGetInfoRgSL(Guid uuid, out DbName entry)
        {
            if (!_database.TryGet(uuid, out entry))
            {
                return false;
            }

            foreach (DbName child in entry.Children)
            {
                if (child.Name == MetadataTokens.InfoRgSL)
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
        private void AddCharacteristicTypes(Guid characteristic, DataTypeDescriptor descriptor)
        {
            _ = _typeDescriptors.TryAdd(characteristic, descriptor);
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

        public bool TryGetExtendedInfo(Guid uuid, out MetadataItemEx info)
        {
            return _extended.TryGetValue(uuid, out info);
        }

        #endregion

        public OneDbMetadataProvider(in Uri uri, bool useExtensions)
        {
            UseExtensions = useExtensions;

            _parsers = new MetadataObjectParserFactory(this);

            _connectionString = DbConnectionFactory.GetConnectionString(in uri);

            if (uri.Scheme == "mssql")
            {
                _provider = DatabaseProvider.SqlServer;
            }
            else if (uri.Scheme == "pgsql")
            {
                _provider = DatabaseProvider.PostgreSql;
            }
            
            Initialize();
        }
        public OneDbMetadataProvider(string connectionString) : this(connectionString, false) { }
        public OneDbMetadataProvider(string connectionString, bool useExtensions)
        {
            _parsers = new MetadataObjectParserFactory(this);

            _provider = connectionString.StartsWith("Host")
                ? DatabaseProvider.PostgreSql
                : DatabaseProvider.SqlServer;

            if (_provider == DatabaseProvider.SqlServer)
            {
                _connectionString = new SqlConnectionStringBuilder(connectionString).ToString();
            }
            else if (_provider == DatabaseProvider.PostgreSql)
            {
                _connectionString = new NpgsqlConnectionStringBuilder(connectionString).ToString();
            }
            else
            {
                _connectionString = connectionString;
            }

            UseExtensions = useExtensions;

            Initialize();
        }
        internal OneDbMetadataProvider(OneDbMetadataProviderOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.UseExtensions && options.Extension is null)
            {
                UseExtensions = options.UseExtensions; // Only the main configuration is allowed to have extensions
            }

            _extension = options.Extension;
            _provider = options.DatabaseProvider;
            _connectionString = options.ConnectionString;

            _parsers = new MetadataObjectParserFactory(this);
        }
        public bool UseExtensions { get; set; } = false;
        public InfoBase InfoBase { get { return _infoBase; } }
        public int YearOffset { get { return _infoBase is null ? 0 : _infoBase.YearOffset; } }
        public ExtensionInfo Extension { get { return _extension; } }
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseProvider DatabaseProvider { get { return _provider; } }
        public IDbConfigurator GetDbConfigurator()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return new MsDbConfigurator(this);
            }
            else if (_provider == DatabaseProvider.PostgreSql)
            {
                return new PgDbConfigurator(this);
            }

            throw new InvalidOperationException($"Unsupported database provider: {_provider}");
        }
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

        #region "INITIALIZE CACHE BEFORE USE IT"

        private Dictionary<Guid, List<Guid>> CreateSupportedMetadataDictionary()
        {
            return new()
            {
                //{ MetadataTypes.Constant,             new List<Guid>() }, // Константы
                //{ MetadataTypes.Subsystem,            new List<Guid>() }, // Подсистемы
                { MetadataTypes.NamedDataTypeDescriptor,     new List<Guid>() }, // Определяемые типы
                { MetadataTypes.SharedProperty,       new List<Guid>() }, // Общие реквизиты
                { MetadataTypes.Catalog,              new List<Guid>() }, // Справочники
                { MetadataTypes.Document,             new List<Guid>() }, // Документы
                { MetadataTypes.Enumeration,          new List<Guid>() }, // Перечисления
                { MetadataTypes.Publication,          new List<Guid>() }, // Планы обмена
                { MetadataTypes.Characteristic,       new List<Guid>() }, // Планы видов характеристик
                { MetadataTypes.InformationRegister,  new List<Guid>() }, // Регистры сведений
                { MetadataTypes.AccumulationRegister, new List<Guid>() }  // Регистры накопления
            };
        }

        internal void Initialize()
        {
            if (!IsNewAgeExtensionsSupported())
            {
                UseExtensions = false;
            }
            InitializeRootFile();
            InitializeDbNameCache();
            InitializeMetadataCache(out _infoBase);
            if (UseExtensions) { ApplyExtensions(); }
        }
        private void InitializeRootFile()
        {
            if (_extension == null)
            {
                using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Config, ConfigFiles.Root))
                {
                    _root = new RootFileParser().Parse(in reader);
                }
            }
            else
            {
                _root = ExtensionRootFileParser.Parse(this, in _extension);
            }
        }
        private void InitializeDbNameCache()
        {
            string fileName = (_extension == null)
                ? ConfigFiles.DbNames
                : ConfigFiles.DbNames + "-ext-" + _extension.Identity.ToString().ToLower();

            using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Params, in fileName))
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

            Dictionary<Guid, List<Guid>> metadata = CreateSupportedMetadataDictionary();

            if (_extension == null)
            {
                using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Config, _root))
                {
                    new InfoBaseParser(this).Parse(in reader, _root, out infoBase, in metadata);
                }
            }
            else
            {
                using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.ConfigCAS, _extension.FileName))
                {
                    new InfoBaseParser(this).Parse(in reader, _extension.Uuid, out infoBase, in metadata);
                }
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

            string fileName;
            string tableName = (_extension == null) ? ConfigTables.Config : ConfigTables.ConfigCAS;

            foreach (var entry in cache.Value)
            {
                if (entry.Key == Guid.Empty)
                {
                    continue;
                }

                fileName = (_extension == null) ? entry.Key.ToString() : _extension.FileMap[entry.Key];

                using (ConfigFileReader reader = new(_provider, in _connectionString, in tableName, in fileName))
                {
                    parser.Parse(in reader, entry.Key, out MetadataInfo metadata);

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
                        AddCharacteristicTypes(metadata.CharacteristicUuid, metadata.DataTypeDescriptor); //FIX: 08.03.2024
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
        private void ApplyExtensions()
        {
            if (_extension is not null) { return; } // Only the main configuration is allowed to have extensions

            List<ExtensionInfo> extensions = GetExtensions();

            foreach (ExtensionInfo extension in extensions)
            {
                if (!TryApplyExtension(in extension, out string error))
                {
                    throw new InvalidOperationException($"Failed to apply extension {extension.Name}: {error}");
                }

                //FIXME: FileName and FileMap is already read
                extension.FileName = null;
                extension.FileMap.Clear();

                if (TryGetMetadata(in extension, out OneDbMetadataProvider metadata, out _))
                {
                    _ = _extensions.TryAdd(extension.Identity, metadata); //FIXME: process errors loading extension
                }
            }
        }

        #endregion

        #region "GETTING METADATA OBJECTS FROM DATABASE"

        private string GetConfigFileName(Guid uuid)
        {
            if (_extension != null)
            {
                return _extension.FileMap[uuid];
            }

            if (_extended.TryGetValue(uuid, out MetadataItemEx item))
            {
                return (item.Uuid == item.Parent) ? item.File : uuid.ToString();
            }

            return uuid.ToString();
        }
        private string GetConfigTableName(Guid uuid)
        {
            if (_extension != null) { return ConfigTables.ConfigCAS; }

            if (uuid == Guid.Empty) { return ConfigTables.Config; }

            if (_extended.TryGetValue(uuid, out MetadataItemEx item))
            {
                return (item.Uuid == item.Parent) ? ConfigTables.ConfigCAS : ConfigTables.Config;
            }

            return ConfigTables.Config;
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

            if (type == MetadataTypes.SharedProperty || type == MetadataTypes.NamedDataTypeDescriptor)
            {
                string tableName = GetConfigTableName(uuid);
                string fileName = GetConfigFileName(uuid);

                using (ConfigFileReader reader = new(_provider, in _connectionString, in tableName, in fileName))
                {
                    parser.Parse(in reader, uuid, out metadata);
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
            string tableName = GetConfigTableName(uuid);
            string fileName = GetConfigFileName(uuid);

            using (ConfigFileReader reader = new(_provider, in _connectionString, in tableName, in fileName))
            {
                parser.Parse(in reader, uuid, out metadata);
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

            if (metadata is ApplicationObject entity)
            {
                if (_extension == null) // Это основная конфигурация, а не расширение
                {
                    Configurator.ConfigureSystemProperties(this, in metadata);
                }
                else if (!string.IsNullOrEmpty(entity.TableName)) // Это собственный объект метаданных расширения
                {
                    //NOTE: Заимствованные объекты метаданных в расширениях
                    //не имеют системных свойств, если они их не переопределяют.
                    Configurator.ConfigureSystemProperties(this, in metadata);
                }

                if (entity is ITablePartOwner)
                {
                    // ConfigureDatabaseNames should be called for the owner first:
                    // the name of table part reference field is dependent on table name of the owner
                    // Owner table name: _Reference1008
                    // Table part name: _Reference1008_VT1023
                    // Table part reference field name: _Reference1008_IDRRef
                    Configurator.ConfigureTableParts(this, in entity);
                }

                ExtendApplicationObject(in entity);
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
        private void ExtendApplicationObject(in ApplicationObject parent)
        {
            if (!TryGetExtendedInfo(parent.Uuid, out MetadataItemEx item))
            {
                return; // Объект не имеет расширения и не является собственным объектом расширения
            }

            if (item.Uuid == item.Parent)
            {
                // Cобственный объект расширения - родительский объект основной конфигурации отсутствует

                if (parent is ITablePartOwner owner)
                {
                    foreach (TablePart table in owner.TableParts)
                    {
                        table.TableName += "x1"; // _Document123_VT45X1
                    }
                }

                parent.TableName += "x1"; // _Document123X1

                return;
            }

            // Заимствованный объект основной конфигурации - требуется применение расширения

            if (!_extensions.TryGetValue(item.Extension, out OneDbMetadataProvider extension))
            {
                return; // This should not happen - extension is not found in the cache!
            }

            MetadataObject entity = extension.GetMetadataObject(item.Type, item.Uuid);

            if (entity == null)
            {
                return; // This should not happen - extent is not found in the extension!
            }

            if (entity is ApplicationObject extent)
            {
                ApplyExtensionProperties(in parent, in extent);

                if (parent is ITablePartOwner parentOwner && extent is ITablePartOwner childOwner)
                {
                    ApplyExtensionTableParts(in parent, in parentOwner, in childOwner);
                }

                parent.TableName += "x1";
            }
        }
        private void ApplyExtensionProperties(in ApplicationObject parent, in ApplicationObject extent)
        {
            foreach (MetadataProperty property in extent.Properties)
            {
                //TODO: применение переопределённых свойств объекта основной конфигурации (parent)

                if (property.Columns.Count > 0) // Собственное свойство расширения
                {
                    parent.Properties.Add(property);
                }
            }
        }
        private void ApplyExtensionTableParts(in ApplicationObject owner, in ITablePartOwner parent, in ITablePartOwner extent)
        {
            foreach (TablePart source in extent.TableParts)
            {
                if (string.IsNullOrEmpty(source.TableName)) // Заимствованная табличная часть расширяемого объекта
                {
                    TablePart target = null;

                    foreach (TablePart table in parent.TableParts)
                    {
                        // Find parent's corresponding table part by uuid or name
                        if (source.Parent == table.Uuid || source.Name == table.Name)
                        {
                            target = table; break;
                        }
                    }

                    if (target == null)
                    {
                        return; // This should not happen - extension table part is not found in the derived object!
                    }

                    foreach (MetadataProperty property in source.Properties)
                    {
                        if (property.Columns.Count > 0) // Собственное свойство расширения заимствованной табличной части
                        {
                            target.Properties.Add(property); break;
                        }
                    }
                }
                else // Собственная табличная часть расширения заимствованного объекта
                {
                    parent.TableParts.Add(source);

                    source.TableName = owner.TableName + source.TableName;

                    foreach (MetadataProperty property in source.Properties)
                    {
                        if (property.Purpose == PropertyPurpose.System && property.Name == "Ссылка" && property.Columns.Count > 0)
                        {
                            property.Columns[0].Name = owner.TableName + "_IDRRef"; break;
                        }
                    }
                }
            }

            foreach (TablePart table in parent.TableParts)
            {
                table.TableName += "x1";
            }
        }

        #endregion

        #region "GETTING METADATA OBJECTS FROM CACHE"

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
                // Основная таблица объекта метаданных
                return GetMetadataObjectCached(type, uuid);
            }

            MetadataObject metadata = GetMetadataObjectCached(type, uuid);

            // Табличная часть или таблица изменений плана обмена

            if (tableName == "Изменения")
            {
                if (metadata is ApplicationObject entity)
                {
                    return GetChangeTrackingTable(entity);
                }
            }

            if (metadata is AccumulationRegister register)
            {
                if (tableName == "Итоги")
                {
                    return GetRegisterTotalsTable(in register);
                }
                else if (tableName == "Настройки")
                {
                    return GetRegisterSettingsTable(in register);
                }
                else
                {
                    return null; // not found error
                }
            }

            if (InfoBase.CompatibilityVersion >= 80302)
            {
                if (metadata is InformationRegister inforeg)
                {
                    if (inforeg.UseSliceFirst || inforeg.UseSliceLast)
                    {
                        if (tableName == "Настройки")
                        {
                            return GetInfoRegisterSettingsTable(in inforeg);
                        }
                        else if (tableName == "СрезПервых")
                        {
                            return GetInfoRegisterSliceFirstTable(in inforeg);
                        }
                        else if (tableName == "СрезПоследних")
                        {
                            return GetInfoRegisterSliceLastTable(in inforeg);
                        }
                        else
                        {
                            return null; // not found error
                        }
                    }
                    else
                    {
                        return null; // not found error
                    }
                }
            }

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
        
        private RegisterTotalsTable GetRegisterTotalsTable(in AccumulationRegister register)
        {
            if (!TryGetAccumRgT(register.Uuid, out _))
            {
                return null;
            }

            RegisterTotalsTable table = new(register);

            Configurator.ConfigureRegisterTotalsTable(this, in table);

            return table;
        }
        private RegisterSettingsTable GetRegisterSettingsTable(in AccumulationRegister register)
        {
            if (!TryGetAccumRgOpt(register.Uuid, out _))
            {
                return null;
            }

            RegisterSettingsTable table = new(register);

            Configurator.ConfigureRegisterSettingsTable(this, in table);

            return table;
        }
        
        private RegisterSettingsTable GetInfoRegisterSettingsTable(in InformationRegister register)
        {
            if (!TryGetInfoRgOpt(register.Uuid, out _))
            {
                return null;
            }

            RegisterSettingsTable table = new(register);

            Configurator.ConfigureInfoRegisterSettingsTable(this, in table);

            return table;
        }
        private RegisterTotalsTable GetInfoRegisterSliceLastTable(in InformationRegister register)
        {
            if (!TryGetInfoRgSL(register.Uuid, out _))
            {
                return null;
            }

            RegisterTotalsTable table = new(register);

            Configurator.ConfigureInfoRegisterSliceLastTable(this, in table);

            return table;
        }
        private RegisterTotalsTable GetInfoRegisterSliceFirstTable(in InformationRegister register)
        {
            if (!TryGetInfoRgSF(register.Uuid, out _))
            {
                return null;
            }

            RegisterTotalsTable table = new(register);

            Configurator.ConfigureInfoRegisterSliceFirstTable(this, in table);

            return table;
        }

        private ApplicationObject GetTypeOrTableDefinitionCached(in string identifier)
        {
            //TODO: try get type definition from cache first

            IDbConfigurator configurator = GetDbConfigurator();

            ApplicationObject udt = null;

            if (identifier == "Метаданные.Объекты")
            {
                udt = configurator.GetTypeDefinition("dajet_md_object");

                if (udt is not null) { udt.TableName = "@md_object"; }
            }
            else if (identifier == "Метаданные.Свойства")
            {
                udt = configurator.GetTypeDefinition("dajet_md_property");

                if (udt is not null) { udt.TableName = "@md_property"; }
            }

            if (udt is not null)
            {
                return udt;
            }

            udt = configurator.GetTypeDefinition(in identifier);

            return udt ?? configurator.GetTableDefinition(in identifier);
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
                /// Single reference type case <see cref="Configurator.ConfigureDataTypeDescriptor"/>
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
        internal bool TryGetCharacteristicDataType(Guid reference, out DataTypeDescriptor descriptor)
        {
            return _typeDescriptors.TryGetValue(reference, out descriptor);
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
        internal Guid GetExtensionObjectParent(Guid uuid)
        {
            //TODO: optimize extensions loading - map child metadata object uuid to parant's one
            //NOTE: собственные объекты расширения Parent == Uuid !!!
            //NOTE: заимствоанный объект расширения Parent != Uuid
            //      Parent = uuid родительского объекта метаданных
            //      Uuid = собственный uuid заимствованного объекта расширения

            foreach (var item in _extended)
            {
                if (item.Value.Uuid == uuid)
                {
                    return item.Key;
                }
            }

            return Guid.Empty; // not found
        }

        #endregion

        #region "RESOLVE DATA TYPE SET REFERENCES"

        /// <summary>
        /// Функция сопоставления ссылочных типов данных объекта "ОписаниеТипов" объектам метаданных.
        /// <br>Список ссылочных типов объекта <see cref="DataTypeDescriptor"/> получает парсер <see cref="DataTypeDescriptorParser"/>.</br>
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
            // 1. DataTypeDescriptor (описание типа данных свойства объекта) может ссылаться только на
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
            //    Согласно алгоритму парсера DataTypeDescriptorParser, они в качестве значения параметра reference отсутствуют.

            if (reference == SingleTypes.ValueStorage)
            {
                return new MetadataItem(SingleTypes.ValueStorage, Guid.Empty, "ХранилищеЗначения");
            }
            else if (reference == SingleTypes.UniqueIdentifier)
            {
                return new MetadataItem(SingleTypes.UniqueIdentifier, Guid.Empty, "УникальныйИдентификатор");
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

        #region "GETTING METADATA OBJECTS INTERFACE IMPLEMENTATION"

        private string[] GetIdentifiers(string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string[] identifiers = metadataName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

            Guid type = MetadataTypes.ResolveName(identifiers[0]);

            if (type == Guid.Empty)
            {
                return GetTypeOrTableDefinitionCached(in metadataName);
            }

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
        public ChangeTrackingTable GetChangeTrackingTable(ApplicationObject entity)
        {
            if (!TryGetChngR(entity.Uuid, out _))
            {
                return null;
            }

            ChangeTrackingTable table = new(entity);

            Configurator.ConfigureChangeTrackingTable(this, in table);

            return table;
        }

        #endregion

        #region "CONFIGURATION EXTENSIONS"

        public List<ExtensionInfo> GetExtensions()
        {
            if (!IsNewAgeExtensionsSupported())
            {
                return new List<ExtensionInfo>();
            }

            if (_provider == DatabaseProvider.SqlServer)
            {
                return MsGetExtensions();
            }
            return PgGetExtensions();
        }
        public ExtensionInfo GetExtension(string name)
        {
            List<ExtensionInfo> list = GetExtensions();

            foreach (ExtensionInfo extension in list)
            {
                if (extension.Name == name)
                {
                    return extension;
                }
            }

            return null;
        }
        private bool IsNewAgeExtensionsSupported()
        {
            IQueryExecutor executor = CreateQueryExecutor();
            return (executor.ExecuteScalar<int>(IS_NEW_AGE_EXTENSIONS_SUPPORTED, 10) == 1);
        }
        private List<ExtensionInfo> MsGetExtensions()
        {
            List<ExtensionInfo> list = new();

            byte[] zippedInfo;

            IQueryExecutor executor = CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(MS_SELECT_EXTENSIONS_NEW, 10))
            {
                zippedInfo = (byte[])reader.GetValue(6);

                Guid uuid = new(DbUtilities.Get1CUuid((byte[])reader.GetValue(0)));

                ExtensionInfo extension = new(this)
                {
                    Identity = uuid, // Поле _IDRRef используется для поиска файла DbNames расширения
                    Order = (int)reader.GetDecimal(1),
                    Name = reader.GetString(2),
                    Updated = reader.GetDateTime(3).AddYears(-InfoBase.YearOffset),
                    Purpose = (ExtensionPurpose)reader.GetDecimal(4),
                    Scope = (ExtensionScope)reader.GetDecimal(5),
                    MasterNode = reader.GetString(7),
                    IsDistributed = (((byte[])reader.GetValue(8))[0] == 1)
                };

                DecodeZippedInfo(in zippedInfo, in extension);

                list.Add(extension);
            }

            return list;
        }
        private List<ExtensionInfo> PgGetExtensions()
        {
            List<ExtensionInfo> list = new();

            byte[] zippedInfo;

            IQueryExecutor executor = CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(PG_SELECT_EXTENSIONS_NEW, 10))
            {
                zippedInfo = (byte[])reader.GetValue(6);

                Guid uuid = new(DbUtilities.Get1CUuid((byte[])reader.GetValue(0)));

                ExtensionInfo extension = new(this)
                {
                    Identity = uuid, // Поле _IDRRef используется для поиска файла DbNames расширения
                    Order = (int)reader.GetDecimal(1),
                    Name = reader.GetString(2),
                    Updated = reader.GetDateTime(3).AddYears(-InfoBase.YearOffset),
                    Purpose = (ExtensionPurpose)reader.GetDecimal(4),
                    Scope = (ExtensionScope)reader.GetDecimal(5),
                    MasterNode = reader.GetString(7),
                    IsDistributed = reader.GetBoolean(8)
                };

                DecodeZippedInfo(in zippedInfo, in extension);

                list.Add(extension);
            }

            return list;
        }
        private void DecodeZippedInfo(in byte[] zippedInfo, in ExtensionInfo extension)
        {
            extension.RootFile = Convert.ToHexString(zippedInfo, 4, 20).ToLower();

            Encoding encoding = (zippedInfo[37] == 0x97) ? Encoding.Unicode : Encoding.ASCII;

            int chars = zippedInfo[38];
            char[] buffer = new char[chars];

            using (MemoryStream stream = new(zippedInfo, 39, zippedInfo.Length - 39))
            {
                using (StreamReader reader = new(stream, encoding))
                {
                    for (int i = 0; i < chars; i++)
                    {
                        buffer[i] = (char)reader.Read();
                    }
                }
            }

            string config = new(buffer);
            int size = encoding.GetByteCount(config);
            byte current = zippedInfo[size + 38]; // '\0'

            using (MemoryStream memory = new(zippedInfo, 39, size))
            {
                using (StreamReader stream = new(memory, encoding))
                {
                    using (ConfigFileReader reader = new(stream))
                    {
                        ConfigObject info = new ConfigFileParser().Parse(reader);

                        extension.Alias = info[2].GetString(2);
                    }
                }
            }

            if (zippedInfo[size + 39] == 0x81) // Значение версии отсутствует
            {
                extension.IsActive = (zippedInfo[size + 40] == 0x82);
            }
            else
            {
                encoding = (zippedInfo[size + 39] == 0x97) ? Encoding.Unicode : Encoding.ASCII;
                chars = zippedInfo[size + 40];
                buffer = new char[chars];

                int offset = size + 41;

                using (MemoryStream stream = new(zippedInfo, offset, zippedInfo.Length - offset))
                {
                    using (StreamReader reader = new(stream, encoding))
                    {
                        for (int i = 0; i < chars; i++)
                        {
                            buffer[i] = (char)reader.Read();
                        }
                    }
                }

                config = new(buffer);
                size = encoding.GetByteCount(config);

                extension.Version = config;
                extension.IsActive = (zippedInfo[offset + size] == 0x82);
            }
        }

        public bool TryGetMetadata(in ExtensionInfo extension, out OneDbMetadataProvider metadata, out string error)
        {
            error = string.Empty;

            metadata = new OneDbMetadataProvider(new OneDbMetadataProviderOptions()
            {
                Extension = extension,
                DatabaseProvider = _provider,
                ConnectionString = _connectionString
            });

            try
            {
                metadata.Initialize();
            }
            catch (Exception exception)
            {
                metadata = null;
                error = ExceptionHelper.GetErrorMessage(exception);
                return false;
            }

            return (metadata != null);
        }

        private IMetadataObjectParser CreateExtensionParser(Guid type)
        {
            if (type == MetadataTypes.Catalog) { return new CatalogParser(); }
            if (type == MetadataTypes.Document) { return new DocumentParser(); }
            if (type == MetadataTypes.Enumeration) { return new EnumerationParser(); }
            if (type == MetadataTypes.Publication) { return new PublicationParser(); }
            if (type == MetadataTypes.Characteristic) { return new CharacteristicParser(); }
            if (type == MetadataTypes.SharedProperty) { return new SharedPropertyParser(); } // since 1C:Enterprise 8.2.14 version
            if (type == MetadataTypes.NamedDataTypeDescriptor) { return new NamedDataTypeDescriptorParser(); } // since 1C:Enterprise 8.3.3 version
            if (type == MetadataTypes.InformationRegister) { return new InformationRegisterParser(); }
            if (type == MetadataTypes.AccumulationRegister) { return new AccumulationRegisterParser(); }

            return null;
        }
        public bool TryApplyExtension(in ExtensionInfo extension, out string error)
        {
            //TODO: extensive error and logging handling !!!

            ConfigFileOptions options = new()
            {
                IsExtension = true,
                FileName = extension.RootFile,
                TableName = ConfigTables.ConfigCAS,
                DatabaseProvider = _provider,
                ConnectionString = _connectionString
            };

            // Initialize extension root file and file map

            if (!ExtensionRootFileParser.TryParse(in options, in extension, out error))
            {
                return false;
            }

            // Initialize extension DbNames

            string fileName = ConfigFiles.DbNames + "-ext-" + extension.Identity.ToString().ToLower();

            DbNameCache database;

            using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.Params, in fileName))
            {
                new DbNamesParser().Parse(in reader, out database);

                _database.AddRange(database.DbNames);
            }

            // Initialize extension metadata types and items list

            Dictionary<Guid, List<Guid>> metadata = CreateSupportedMetadataDictionary();
            
            using (ConfigFileReader reader = new(_provider, in _connectionString, ConfigTables.ConfigCAS, extension.FileName))
            {
                new InfoBaseParser(this).Parse(in reader, extension.Uuid, out _, in metadata);
            }

            foreach (var type in metadata)
            {
                IMetadataObjectParser parser = CreateExtensionParser(type.Key);

                if (parser is null) { continue; }
                if (type.Key == MetadataTypes.SharedProperty) { continue; }
                if (type.Key == MetadataTypes.NamedDataTypeDescriptor) { continue; } //TODO: (!) заполнить коллекцию OneDbMetadataProvider._references.
                
                foreach (var uuid in type.Value)
                {
                    if (!extension.FileMap.TryGetValue(uuid, out fileName)) { continue; }

                    if (string.IsNullOrWhiteSpace(fileName)) { continue; }

                    options.FileName = fileName;
                    options.MetadataUuid = uuid;

                    if (!ExtendsDatabaseSchema(in database, in options, in parser)) { continue; }

                    parser.Parse(in options, out MetadataInfo info);

                    MetadataItemEx item = new(extension.Identity, info.MetadataType, info.MetadataUuid, info.Name, fileName, info.MetadataParent);

                    ApplyMetadataObjectExtension(in item);
                }
            }

            return string.IsNullOrEmpty(error);
        }
        private bool ExtendsDatabaseSchema(in DbNameCache database, in ConfigFileOptions options, in IMetadataObjectParser parser)
        {
            if (database.TryGet(options.MetadataUuid, out _))
            {
                return true; // Собственный объект расширения
            }

            MetadataObject metadata;

            using (ConfigFileReader reader = new(options.DatabaseProvider, options.ConnectionString, options.TableName, options.FileName))
            {
                parser.Parse(in reader, options.MetadataUuid, out metadata);
            }

            if (metadata is not ApplicationObject entity)
            {
                return false; // Utility metadata objects do not change the database schema
            }

            foreach (MetadataProperty property in entity.Properties)
            {
                if (database.TryGet(property.Uuid, out _))
                {
                    return true; // Собственное свойство расширения заимствованного объекта основной конфигурации
                }
            }

            if (entity is ITablePartOwner owner)
            {
                foreach (TablePart table in owner.TableParts)
                {
                    if (database.TryGet(table.Uuid, out _))
                    {
                        return true; // Собственная табличная часть расширения заимствованного объекта основной конфигурации
                    }

                    foreach (MetadataProperty property in table.Properties)
                    {
                        if (database.TryGet(property.Uuid, out _))
                        {
                            return true; // Собственное свойство табличной части заимствованного объекта основной конфигурации
                        }
                    }
                }
            }

            return false;
        }
        private void ApplyMetadataObjectExtension(in MetadataItemEx item)
        {
            //TODO: (!) заполнить коллекции _references, _characteristics, _owners и _registers.

            if (!TryApplyExtensionByUuid(in item)) { ApplyExtensionByName(in item); }
        }
        private bool TryApplyExtensionByUuid(in MetadataItemEx item)
        {
            if (!_cache.TryGetValue(item.Type, out Dictionary<Guid, WeakReference<MetadataObject>> items))
            {
                return false; // Основная конфигурация не содержит данный тип метаданных
            }

            if (!items.ContainsKey(item.Parent))
            {
                return false; // Основная конфигурация не содержит объект метаданных с таким uuid - нужно применять по имени
            }

            _ = _extended.TryAdd(item.Parent, item);

            return true;
        }
        private void ApplyExtensionByName(in MetadataItemEx item)
        {
            if (_names.TryGetValue(item.Type, out Dictionary<string, Guid> names))
            {
                // Основная конфигурация содержит данный тип метаданных - сопоставляем по имени

                if (names.TryGetValue(item.Name, out Guid parent))
                {
                    // Заимствованный из основной конфигурации объект

                    _ = _extended.TryAdd(parent, item.SetParent(parent));
                }
                else
                {
                    // Собственный объект расширения - новый для основной конфигурации

                    _cache[item.Type].Add(item.Uuid, new WeakReference<MetadataObject>(null));

                    AddName(item.Type, item.Uuid, item.Name);

                    _ = _extended.TryAdd(item.Uuid, item.SetParent(item.Uuid));
                }
            }
            else // Основная конфигурация не содержит данный тип метаданных - добавляем объект метаданных
            {
                // Собственный объект расширения - новый для основной конфигурации

                _ = _cache.TryAdd(item.Type, new Dictionary<Guid, WeakReference<MetadataObject>>()
                {
                    { item.Uuid, new WeakReference<MetadataObject>(null) }
                });
                
                AddName(item.Type, item.Uuid, item.Name);
                
                _ = _extended.TryAdd(item.Uuid, item.SetParent(item.Uuid));
            }
        }
        #endregion

        #region "USER API METHODS"

        /// <summary>
        /// Получает внутренний индентификатор значения перечисления или
        /// <br>предопределённого значения справочника по его полному имени</br>
        /// <br><b>Например:</b> "Перечисление.СтавкиНДС.БезНДС"</br>
        /// </summary>
        /// <param name="enumValueFullName"></param>
        /// <returns>Возвращает внутренний индентификатор значения</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Guid GetEnumValue(in string valueFullName)
        {
            string[] identifiers = GetIdentifiers(valueFullName);

            if (identifiers.Length != 3)
            {
                throw new FormatException(nameof(valueFullName));
            }

            string valueName = identifiers[2];
            string metadataName = string.Join(".", identifiers[0], identifiers[1]);

            MetadataObject entity = GetMetadataObject(metadataName);

            if (entity == null)
            {
                throw new InvalidOperationException($"Объект метаданных \"{metadataName}\" не найден.");
            }

            if (entity is Enumeration enumeration)
            {
                foreach (EnumValue value in enumeration.Values)
                {
                    if (value.Name == valueName)
                    {
                        return value.Uuid;
                    }
                }
                throw new InvalidOperationException($"Значение [{valueName}] перечисления \"{metadataName}\" не найдено.");
            }
            else if (entity is Catalog catalog)
            {
                if (TryGetPredefinedValue(catalog.PredefinedValues, in valueName, out PredefinedValue value))
                {
                    return value.Uuid;
                }
                throw new InvalidOperationException($"Предопределённое значение [{valueName}] справочника \"{metadataName}\" не найдено.");
            }

            throw new InvalidOperationException($"Предопределённые значения для объекта метаданных \"{metadataName}\" не поддерживаются.");
        }
        public bool TryGetEnumValue(in string valueFullName, out EnumValue result)
        {
            result = null;

            string[] identifiers = GetIdentifiers(valueFullName);

            if (identifiers is null || identifiers.Length != 3) { return false; }

            string valueName = identifiers[2];
            string metadataName = string.Join(".", identifiers[0], identifiers[1]);

            MetadataObject entity = GetMetadataObject(metadataName);

            if (entity == null) { return false; }

            if (entity is Enumeration enumeration)
            {
                foreach (EnumValue value in enumeration.Values)
                {
                    if (value.Name == valueName)
                    {
                        result = value; return true;
                    }
                }
            }

            return false;
        }
        private bool TryGetPredefinedValue(in List<PredefinedValue> values, in string name, out PredefinedValue result)
        {
            result = null;

            foreach (PredefinedValue value in values)
            {
                if (value.Name == name)
                {
                    result = value; return true;
                }
            }

            foreach (PredefinedValue value in values)
            {
                if (TryGetPredefinedValue(value.Children, in name, out result))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}

// Системная таблица "Метаданные.Объекты"
// **************************************
// CREATE TYPE dajet_md_object
// (
//   Ссылка   uuid,
//   Код      number(5),
//   Тип      string(32),
//   Имя      string(128),
//   Таблица  string(128),
//   Владелец uuid
// )

// Системная таблица "Метаданные.Свойства"
// ***************************************
// CREATE TYPE dajet_md_property
// (
//   Ссылка   uuid,
//   Тип      string(32),
//   Имя      string(128),
//   Колонка  string(128),
//   Владелец uuid
// )