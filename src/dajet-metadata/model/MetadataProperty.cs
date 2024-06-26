﻿using System.Collections.Generic;
using System.Linq;

namespace DaJet.Metadata.Model
{
    ///<summary>Класс для описания свойств объекта метаданных <see cref="ApplicationObject"> (реквизитов, измерений и ресурсов)</summary>
    public class MetadataProperty : MetadataObject
    {
        ///<summary>Основа имени поля в таблице СУБД (может быть дополнено постфиксами в зависимости от типа данных свойства)</summary>
        public string DbName { get; set; } = string.Empty;
        ///<summary>Признак вхождения в состав первичного ключа прикладного объекта
        ///<br/>Значение = 0 - не входит в состав первичного ключа.
        ///<br/>Значение > 0 - порядковый номер в составе первичного ключа.
        ///</summary>
        public int PrimaryKey { get; set; } = 0;
        ///<summary>Значение свойства генерируется средствами СУБД автоматически при выполнении команды INSERT.</summary>
        public bool IsDbGenerated { get; set; } = false;
        ///<summary>Коллекция для описания полей таблицы СУБД свойства объекта метаданных</summary>
        public List<MetadataColumn> Columns { get; set; } = new List<MetadataColumn>();
        ///<summary>Логический смысл свойства. Подробнее смотри перечисление <see cref="PropertyPurpose"/>.</summary>
        public PropertyPurpose Purpose { get; set; } = PropertyPurpose.Property;
        ///<summary>Описание типов данных <see cref="DataTypeDescriptor"/>, которые могут использоваться для значений свойства.</summary>
        public DataTypeDescriptor PropertyType { get; set; } = new DataTypeDescriptor();
        ///<summary>Описание типов данных свойства, определённых в расширении.</summary>
        public DataTypeDescriptor ExtensionPropertyType { get; set; } = new DataTypeDescriptor();
        /// <summary>Вариант использования реквизита для групп и элементов</summary>
        public PropertyUsage PropertyUsage { get; set; } = PropertyUsage.Item;
        /// <summary>Использование измерения периодического или непереодического регистра сведений,
        /// <br>который не подчинён регистратору, в качестве основного отбора при регистрации изменений в плане обмена</br></summary>
        public bool UseForChangeTracking { get; set; } = false;
        /// <summary>Признак измерения регистра сведений (ведущее):
        /// <br>запись будет подчинена объектам, записываемым в данном измерении</br></summary>
        public bool CascadeDelete { get; set; } = false; // Каскадное удаление по внешнему ключу
        public override string ToString() { return Name; }
    }
}