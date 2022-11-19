using DaJet.Metadata.Model;
using System;

namespace DaJet.Metadata.Extensions
{
    /// <summary>
    /// Доступен, начиная с версии 8.3.6
    /// </summary>
    public sealed class ExtensionInfo : MetadataObject
    {
        /// <summary>
        /// Если данное свойство установлено в значение "Ложь", то при следующем запуске
        /// <br>расширение не будет подключено. (Доступно, начиная с версии 8.3.12)</br>
        /// </summary>
        public bool IsActive { get; set; }
        public string Version { get; set; } // Доступно, начиная с версии 8.3.6
        public int Order { get; set; }
        public ExtensionScope Scope { get; set; } = ExtensionScope.InfoBase;
        public ExtensionPurpose Purpose { get; set; } = ExtensionPurpose.Customization;
        /// <summary>
        /// Это значение является значением поля "FileName" таблицы "ConfigCAS".
        /// <br>Вычисляется по алгоритму SHA-1 по значению поля "BinaryData" таблицы "ConfigCAS".</br>
        /// </summary>
        public string FileName { get; set; } // TODO: re-name ?
        public string RootFile { get; set; } // TODO: re-name ?
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