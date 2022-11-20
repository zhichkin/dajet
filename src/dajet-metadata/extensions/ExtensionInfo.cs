using DaJet.Metadata.Model;
using System;

namespace DaJet.Metadata.Extensions
{
    /// <summary>
    /// Расширение конфигурации (доступно, начиная с версии 8.3.6)
    /// </summary>
    public sealed class ExtensionInfo : MetadataObject
    {
        /// <summary>
        /// Если данное свойство установлено в значение "Ложь", то при следующем запуске
        /// <br>расширение не будет подключено. (Доступно, начиная с версии 8.3.12)</br>
        /// </summary>
        public bool IsActive { get; set; }
        /// <summary>Версия расширения (доступно, начиная с версии 8.3.6)</summary>
        public string Version { get; set; }
        /// <summary>Порядковый номер расширения</summary>
        public int Order { get; set; }
        /// <summary>Область действия расширения (доступно, начиная с версиии 8.3.12)</summary>
        public ExtensionScope Scope { get; set; } = ExtensionScope.InfoBase;
        /// <summary>Назначение расширения (доступно, начиная с версиии 8.3.10)</summary>
        public ExtensionPurpose Purpose { get; set; } = ExtensionPurpose.Customization;
        /// <summary>
        /// Это значение является значением поля "FileName" таблицы "ConfigCAS".
        /// <br>Вычисляется по алгоритму SHA-1 по значению поля "BinaryData" таблицы "ConfigCAS".</br>
        /// </summary>
        public string RootFile { get; set; }
        /// <summary>
        /// Это значение является значением поля "FileName" таблицы "ConfigCAS".
        /// <br>Имя файла описания объектов метаданных, входящих в состав расширения.</br>
        /// </summary>
        public string FileName { get; set; }
        /// <summary>Дата и время последнего обновления расширения</summary>
        public DateTime Updated { get; set; }
        /// <summary>
        /// Содержит узел распределенной информационной базы, в котором создано данное расширение конфигурации.
        /// <br>Если текущая информационная база не является узлом распределенной информационной базы или</br>
        /// <br>расширение создано локально, то содержит значение "Неопределено". (Доступно, начиная с версии 8.3.12)</br>
        /// </summary>
        public string MasterNode { get; set; } = "0:00000000000000000000000000000000";
        /// <summary>
        /// Если это свойство установлено в значение "Истина", то данное расширение конфигурации
        /// <br>будет передаваться в распределенных информационных базах, организованных планами обмена,</br>
        /// <br>у которых свойство "РаспределеннаяИнформационнаяБаза" установлено в значение "Истина".</br>
        /// </summary>
        public bool IsDistributed { get; set; } // Доступно, начиная с версии 8.3.12
    }
}

// *********************************************************
// * Порядок применения расширений к текущей конфигурации: *
// *********************************************************
// SELECT
//   T1._IDRRef,
//   T1._ExtensionOrder,
//   T1._ExtName,
//   T1._UpdateTime,
//   T1._ExtensionUsePurpose,
//   T1._ExtensionScope,
//   T1._ExtensionZippedInfo,
//   T1._MasterNode,
//   T1._UsedInDistributedInfoBase,
//   T1._Version
// FROM dbo._ExtensionsInfo T1
// ORDER BY
//   CASE WHEN SUBSTRING(T1._MasterNode, CAST(1.0 AS INT), CAST(34.0 AS INT)) = N'0:00000000000000000000000000000000'
//        THEN 0x01
//        ELSE 0x00
//   END,
//   T1._ExtensionUsePurpose,
//   T1._ExtensionScope,
//   T1._ExtensionOrder;