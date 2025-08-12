using System;
using System.Collections.Generic;

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

        #region "Регистр бухгалтерии"
        ///<summary><b>Использование отдельных свойств для дебета и кредита:</b>
        ///<br>true - не использовать</br>
        ///<br>false - использовать</br></summary>
        public bool IsBalance { get; set; } = true; // Балансовый (измерения и ресурсы)
        ///<summary>Признак учёта (для счёта плана счетов)</summary>
        public Guid AccountingFlag { get; set; } = Guid.Empty; // Признак учёта (измерения и ресурсы)
        ///<summary><b>Признак учёта субконто</b>
        ///<br>Используется в стандартной (системной) табличной части "ВидыСубконто"</br>
        ///<br>если <see cref="Account.MaxDimensionCount"/> больше нуля.</br></summary>
        public Guid AccountingDimensionFlag { get; set; } = Guid.Empty; // Признак учёта субконто (только ресурсы)
        #endregion

        #region "Задача"
        ///<summary>Ссылка на измерение регистра сведений,
        ///<br>указанного в свойстве "Адресация" задачи</br></summary>
        public Guid RoutingDimension { get; set; } = Guid.Empty; // Измерение адресации
        #endregion

        public MetadataProperty Copy()
        {
            MetadataProperty copy = new()
            {
                Uuid = this.Uuid,
                Name = this.Name,
                Alias = this.Alias,
                Comment = this.Comment,
                Parent = this.Parent,
                ExtensionPropertyType = this.ExtensionPropertyType?.Copy(),
                DbName = this.DbName,
                Purpose = this.Purpose,
                PropertyType = this.PropertyType?.Copy(),
                PrimaryKey = this.PrimaryKey,
                IsDbGenerated = this.IsDbGenerated,
                PropertyUsage = this.PropertyUsage,
                CascadeDelete = this.CascadeDelete,
                UseForChangeTracking = this.UseForChangeTracking,
                IsBalance = this.IsBalance,
                AccountingFlag = this.AccountingFlag,
                AccountingDimensionFlag = this.AccountingDimensionFlag,
                RoutingDimension = this.RoutingDimension
            };

            foreach (Guid reference in this.References)
            {
                copy.References.Add(reference);
            }

            for (int i = 0; i < this.Columns.Count; i++)
            {
                copy.Columns.Add(this.Columns[i].Copy());
            }

            return copy;
        }

        public override string ToString() { return Name; }

        ///<summary>
        ///<b>Свойство определяет логическую ссылочную целостность базы данных (foreign keys)</b>
        ///<br>Содержит список идентификаторов ссылочных типов данных, которые определяются</br>
        ///<br>в типе свойства, в том числе составного типа, "ОписаниеТипов" или "Характеристика".</br>
        ///<br><b>Возможные типы данных:</b></br>
        ///<br>- ХранилищеЗначения (константа - исключение из правил)</br>
        ///<br>- УникальныйИдентификатор  (константа - исключение из правил)</br>
        ///<br>- Характеристика <see cref="OneDbMetadataProvider._characteristics"/></br>
        ///<br>- ОпределяемыйТип <see cref="OneDbMetadataProvider._references"/></br>
        ///<br>- Общие ссылочные типы, например, ЛюбаяСсылка или СправочникСсылка</br>
        ///<br>- Конкретные ссылочные типы, например, СправочникСсылка.Номенклатура</br>
        ///<br>Функция для обработки идентификаторов: <b>Configurator.ConfigureDataTypeDescriptor</b></br>
        ///<br>Функция для разрешения идентификаторов: <b>Configurator.ResolveReferencesToMetadataItems</b></br>
        ///</summary>
        public List<Guid> References { get; } = [];
    }
}