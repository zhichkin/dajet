using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    ///<summary>Прикладной объект метаданных (справочник, документ, регистр и т.п.)</summary>
    public abstract class ApplicationObject : MetadataObject
    {
        ///<summary>
        ///Идентификатор объекта метаданных для использования на уровне СУБД (см. файл DBNames таблицы Params).
        ///Используется для формирования названий объектов СУБД, а также в качестве значения дискриминатора типа составных полей.
        ///</summary>
        public int TypeCode { get; set; }
        ///<summary>
        ///Имя основной таблицы СУБД объекта метаданных, где хранятся пользовательские данные прикладного решения.
        ///</summary>
        public string TableName { get; set; }
        ///<summary>
        ///Свойства прикладного объекта метаданных.
        ///</summary>
        public List<MetadataProperty> Properties { get; set; } = new List<MetadataProperty>();
    }
}