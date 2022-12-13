using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Core
{
    ///<summary>
    ///Структура описания объекта метаданных.
    ///<br>Используется специализированными парсерами файлов конфигурации</br>
    ///<br>для загрузки первичных данных объектов метаданных и заполнения кэша.</br>
    ///</summary>
    public sealed class MetadataInfo
    {
        public MetadataInfo() { }
        ///<summary>Имя объекта метаданных</summary>
        public string Name { get; set; } = string.Empty;
        ///<summary>
        ///UUID общего типа метаданных
        ///<br>Пример: "Справочник"</br>
        ///</summary>
        public Guid MetadataType { get; set; } = Guid.Empty;
        ///<summary>
        ///UUID объекта метаданных
        ///<br>Пример: "Справочник.Номенклатура"</br>
        ///</summary>
        public Guid MetadataUuid { get; set; } = Guid.Empty;
        ///<summary>Идентификатор расширяемого объекта метаданных основной конфигурации
        ///<br>Используется при синхронизации объектов расширения по внутренним идентификаторам</br>
        ///<br>Смотри также: <see cref="InfoBase.MapMetadataByUuid"/></br>
        ///</summary>
        public Guid MetadataParent { get; set; } = Guid.Empty;
        ///<summary>
        ///UUID ссылочного типа данных "Ссылка"
        ///<br>Пример: "СправочникСсылка.Номенклатура"</br>
        ///</summary>
        public Guid ReferenceUuid { get; set; } = Guid.Empty;
        ///<summary>
        ///UUID ссылочного типа данных "Характеристика" (тип значения характеристик)
        ///<br>Пример: "ПланВидовХарактеристик.ВидыСубконтоХозрасчетные.Характеристика"</br>
        ///</summary>
        public Guid CharacteristicUuid { get; set; } = Guid.Empty;
        ///<summary>
        ///Список владельцев <see cref="Catalog"/> подчинённого справочника
        ///</summary>
        public List<Guid> CatalogOwners { get; set; } = new List<Guid>();
        ///<summary>
        ///Список регистров движения документа <see cref="Document"/>
        ///<br>Регистры движения: <see cref="InformationRegister"/> или <see cref="AccumulationRegister"/></br>
        ///</summary>
        public List<Guid> DocumentRegisters { get; set; } = new List<Guid>();
    }
}