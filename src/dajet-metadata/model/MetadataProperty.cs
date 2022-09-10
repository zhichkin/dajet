using System.Collections.Generic;
using System.Linq;

namespace DaJet.Metadata.Model
{
    ///<summary>Класс для описания свойств объекта метаданных <see cref="ApplicationObject"> (реквизитов, измерений и ресурсов)</summary>
    public class MetadataProperty : MetadataObject
    {
        ///<summary>Основа имени поля в таблице СУБД (может быть дополнено постфиксами в зависимости от типа данных свойства)</summary>
        public string DbName { get; set; } = string.Empty;
        ///<summary>Коллекция для описания полей таблицы СУБД свойства объекта метаданных</summary>
        public List<MetadataColumn> Columns { get; set; } = new List<MetadataColumn>();
        ///<summary>Логический смысл свойства. Подробнее смотри перечисление <see cref="PropertyPurpose"/>.</summary>
        public PropertyPurpose Purpose { get; set; } = PropertyPurpose.Property;
        ///<summary>Описание типов данных <see cref="DataTypeSet"/>, которые могут использоваться для значений свойства.</summary>
        public DataTypeSet PropertyType { get; set; } = new DataTypeSet();
        /// <summary>Вариант использования реквизита для групп и элементов</summary>
        public PropertyUsage PropertyUsage { get; set; } = PropertyUsage.Item;
        /// <summary>Использование измерения периодического или непереодического регистра сведений,
        /// <br>который не подчинён регистратору, в качестве основного отбора при регистрации изменений в плане обмена</br></summary>
        public bool DimensionUsage { get; set; } = false;
        /// <summary>Признак измерения регистра сведений (ведущее):
        /// <br>запись будет подчинена объектам, записываемым в данном измерении</br></summary>
        public bool CascadeDelete { get; set; } = false; // Каскадное удаление по внешнему ключу
        public bool IsPrimaryKey()
        {
            return (Columns != null
                && Columns.Count > 0
                && Columns.Where(f => f.IsPrimaryKey).FirstOrDefault() != null);
        }
        public override string ToString() { return Name; }
    }
}