namespace DaJet.Metadata.Model
{
    // Запрещена комбинация основного режима запуска "Управляемое приложение" и режима совместимости с версией 8.1
    // Включение режима совместимости с версией 8.3.9 и ниже не совместимо с выключенным разделением расширений у общего реквизита
    // Включение режима совместимости с версией 8.2.13 и ниже несовместимо с наличием в конфигурации общих реквизитов
    // Использование определяемых типов в режиме совместимости 8.3.2 и ниже недопустимо
    public sealed class InfoBase : MetadataObject
    {
        /// <summary>
        /// Версия среды выполнения платформы
        /// </summary>
        public int PlatformVersion { get; set; }
        /// <summary>
        /// Режим совместимости платформы
        /// </summary>
        public int CompatibilityVersion { get; set; }
        /// <summary>
        /// Версия прикладной конфигурации
        /// </summary>
        public string AppConfigVersion { get; set; } = string.Empty;
        /// <summary>
        /// Смещение дат
        /// </summary>
        public int YearOffset { get; set; }
        /// <summary>
        /// Режим использования синхронных вызовов расширений платформы и внешних компонент
        /// </summary>
        public SyncCallsMode SyncCallsMode { get; set; }
        /// <summary>
        /// Режим управления блокировкой данных
        /// </summary>
        public DataLockingMode DataLockingMode { get; set; }
        /// <summary>
        /// Режим использования модальности
        /// </summary>
        public ModalWindowMode ModalWindowMode { get; set; }
        /// <summary>
        /// Режим автонумерации объектов
        /// </summary>
        public AutoNumberingMode AutoNumberingMode { get; set; }
        /// <summary>
        /// Режим совместимости интерфейса
        /// </summary>
        public UICompatibilityMode UICompatibilityMode { get; set; }
        /// <summary>
        /// Префикс имен собственных объектов расширения конфигурации
        /// </summary>
        public string NamePrefix { get; set; }
        /// <summary>
        /// Поддерживать соответствие объектам расширяемой конфигурации по внутренним идентификаторам
        /// </summary>
        public bool MapMetadataByUuid { get; set; } = true;
        /// <summary>
        /// Режим совместимости расширения конфигурации
        /// </summary>
        public int ExtensionCompatibility { get; set; }
    }
}