using DaJet.Metadata.Model;
using System.Collections.Generic;

namespace DaJet.Metadata.Services
{
    /// <summary>
    /// Интерфейс для сравнения и слияния объектов метаданных
    /// </summary>
    public interface IMetadataCompareAndMergeService
    {
        /// <summary>
        /// Подготавливает список полей таблицы СУБД для сравнения
        /// </summary>
        /// <param name="fields">Список полей таблицы СУБД отсортированный по их имени</param>
        /// <returns>Список имён полей таблицы СУБД, отсортированный по возрастанию</returns>
        List<string> PrepareComparison(List<SqlFieldInfo> fields);
        /// <summary>
        /// Подготавливает список полей свойств объекта метаданных для сравнения
        /// </summary>
        /// <param name="properties">Список свойств объекта метаданных</param>
        /// <returns>Список имён полей свойств объекта метаданных, отсортированный по возрастанию</returns>
        List<string> PrepareComparison(List<MetadataProperty> properties);
        /// <summary>
        /// Выполняет сравнение отсортированных по возврастанию списков: обновляемого (target) и эталонного (source).
        /// </summary>
        /// <param name="target_list">Список для изменения (обновляемый)</param>
        /// <param name="source_list">Список для сравнения (эталонный)</param>
        /// <param name="delete_list">Значения для удаления из обновляемого списка</param>
        /// <param name="insert_list">Значения для добавления в обновляемый список</param>
        void Compare(List<string> target_list, List<string> source_list, out List<string> delete_list, out List<string> insert_list);
        
        
        
        /// <summary>
        /// Выполняет сравнение и слияние свойств объекта метаданных с полями таблицы СУБД
        /// </summary>
        /// <param name="metaObject">Объект метаданных, для которого выполняется сравнение и слияние</param>
        /// <param name="fields">Коллекция полей таблицы СУБД объекта метаданных, отсортированная по возрастанию имён полей</param>
        //void MergeProperties(ApplicationObject metaObject, List<SqlFieldInfo> fields);
    }
    /// <summary>
    /// Класс реализует интерфейс <see cref="IMetadataCompareAndMergeService"/>,
    /// выполняющий сравнение и слияние объектов метаданных по их именам
    /// </summary>
    public sealed class MetadataCompareAndMergeService : IMetadataCompareAndMergeService
    {
        //private readonly IMetadataManager MetadataManager = new MetadataManager();
        public void Compare(List<string> target_list, List<string> source_list, out List<string> delete_list, out List<string> insert_list)
        {
            delete_list = new List<string>();
            insert_list = new List<string>();

            int target_count = target_list.Count;
            int source_count = source_list.Count;
            int target_index = 0;
            int source_index = 0;
            int compareResult;

            if (target_count == 0 && source_count == 0) return;

            //target_list.Sort();
            //source_list.Sort();

            while (target_index < target_count) // target список "ведущий"
            {
                if (source_index < source_count) // в source списке ещё есть элементы
                {
                    compareResult = target_list[target_index].CompareTo(source_list[source_index]);
                    if (compareResult < 0) // target меньше source
                    {
                        // Элемент target больше не может встретиться в списке source.
                        delete_list.Add(target_list[target_index]);
                        // Добавлять элемент source в список insert мы пока не можем,
                        // так как элемент source возможно есть ниже в списке target.
                        target_index++; // Берём следующий элемент target
                    }
                    else if (compareResult == 0) // target равен source
                    {
                        target_index++; // Берём следующий элемент target
                        source_index++; // Берём следующий элемент source
                    }
                    else // target больше source
                    {
                        // Добавлять элемент target в список delete мы пока не можем,
                        // так как элемент target возможно есть ниже в списке source.
                        // Элемент source больше не может встретиться в списке target.
                        insert_list.Add(source_list[source_index]);
                        source_index++; // Берём следующий элемент source
                    }
                }
                else // достигли конца source списка
                {
                    delete_list.Add(target_list[target_index]);
                    target_index++; // Берём следующий элемент target
                }
            }
            while (source_index < source_count) // target список оказался короче source списка
            {
                // Добавляем все оставшиеся элементы source
                insert_list.Add(source_list[source_index]);
                source_index++; // Берём следующий элемент source
            }
        }
        public List<string> PrepareComparison(List<SqlFieldInfo> fields)
        {
            List<string> list = new List<string>(fields.Count);
            
            for (int i = 0; i < fields.Count; i++)
            {
                list.Add(fields[i].COLUMN_NAME.ToLowerInvariant());
            }
            
            return list;
        }
        public List<string> PrepareComparison(List<MetadataProperty> properties)
        {
            List<string> list = new(properties.Count);
            
            for (int i = 0; i < properties.Count; i++)
            {
                foreach (var field in properties[i].Columns)
                {
                    list.Add(field.Name.ToLowerInvariant());
                }
            }
            
            list.Sort();

            return list;
        }

        #region "TODO: MERGE PROPERTIES SERVICE"

        //private SortedDictionary<string, MetadataProperty> PrepareMerging(List<MetadataProperty> properties)
        //{
        //    SortedDictionary<string, MetadataProperty> fields = new SortedDictionary<string, MetadataProperty>();
        //    for (int p = 0; p < properties.Count; p++)
        //    {
        //        for (var f = 0; f < properties[p].Fields.Count; f++)
        //        {
        //            fields.Add(properties[p].Fields[f].Name.ToLowerInvariant(), properties[p]);
        //        }
        //    }
        //    return fields;
        //}
        //public void MergeProperties(ApplicationObject metaObject, List<SqlFieldInfo> fields)
        //{
        //    SortedDictionary<string, MetadataProperty> lookup = PrepareMerging(metaObject.Properties);

        //    List<string> target_list = lookup.Keys.ToList();

        //    int source_count = fields.Count;
        //    int target_count = target_list.Count;
        //    if (target_count == 0 && source_count == 0) return;

        //    int target_index = 0;
        //    int source_index = 0;
        //    int compareResult;

        //    while (target_index < target_count)
        //    {
        //        if (source_index < source_count)
        //        {
        //            compareResult = target_list[target_index].CompareTo(fields[source_index].COLUMN_NAME.ToLowerInvariant());
        //            if (compareResult < 0) // target меньше source = delete
        //            {
        //                DeletePropertyField(metaObject.Properties, lookup[target_list[target_index]], target_list[target_index]);
        //                target_index++;
        //            }
        //            else if (compareResult == 0) // target равен source = update
        //            {
        //                UpdatePropertyField(lookup[fields[source_index].COLUMN_NAME.ToLowerInvariant()], fields[source_index]);
        //                target_index++;
        //                source_index++;
        //            }
        //            else // target больше source = insert
        //            {
        //                // Добавлять элемент target в список delete мы пока не можем,
        //                // так как элемент target возможно есть ниже в списке source.
        //                // Элемент source больше не может встретиться в списке target.
        //                InsertPropertyField(metaObject, fields[source_index]);
        //                source_index++; // Берём следующий элемент source
        //            }
        //        }
        //        else // достигли конца source списка
        //        {
        //            // Удаляем все оставшиеся поля объекта метаданных
        //            DeletePropertyField(metaObject.Properties, lookup[target_list[target_index]], target_list[target_index]);
        //            target_index++;
        //        }
        //    }
        //    while (source_index < source_count)
        //    {
        //        // Добавляем все оставшиеся поля в свойства объекта метаданных
        //        InsertPropertyField(metaObject, fields[source_index]);
        //        source_index++; // Берём следующий элемент source
        //    }
        //}
        //private void DeletePropertyField(List<MetadataProperty> properties, MetadataProperty property, string name)
        //{
        //    for (int i = 0; i < property.Fields.Count; i++)
        //    {
        //        if (property.Fields[i].Name.ToLowerInvariant() == name)
        //        {
        //            property.Fields.RemoveAt(i);
        //            break;
        //        }
        //    }
        //    if (property.Fields.Count == 0)
        //    {
        //        properties.Remove(property);
        //    }
        //}
        //private void UpdatePropertyField(MetadataProperty property, SqlFieldInfo field)
        //{
        //    if (field.DATA_TYPE != "nchar"
        //        && field.DATA_TYPE.StartsWith("mchar")
        //        && field.DATA_TYPE != "nvarchar"
        //        && field.DATA_TYPE.StartsWith("mvarchar")
        //        && field.DATA_TYPE != "numeric") return;

        //    MetadataColumn f = property.Fields.Where(f => f.Name == field.COLUMN_NAME).FirstOrDefault();
        //    if (f == null) return;
        //    f.TypeName = field.DATA_TYPE;
        //    f.Length = field.CHARACTER_OCTET_LENGTH;
        //    f.Scale = field.NUMERIC_SCALE;
        //    f.Precision = field.NUMERIC_PRECISION;
        //    f.IsNullable = field.IS_NULLABLE;
        //}
        //private void InsertPropertyField(ApplicationObject metaObject, SqlFieldInfo fieldInfo)
        //{
        //    IApplicationObjectFactory factory = MetadataManager.GetFactory(metaObject.GetType());
        //    if (factory == null) return;

        //    string propertyName = factory.PropertyFactory.GetPropertyName(fieldInfo);
        //    if (string.IsNullOrEmpty(propertyName))
        //    {
        //        propertyName = fieldInfo.COLUMN_NAME;
        //    }

        //    // Проверка нужна для свойств, имеющих составной тип данных
        //    MetadataProperty property = metaObject.Properties.Where(p => p.Name == propertyName).FirstOrDefault();
        //    if (property == null)
        //    {
        //        property = factory.PropertyFactory.CreateProperty(metaObject, propertyName, fieldInfo);
        //        metaObject.Properties.Add(property);
        //    }
        //    else if (property.Fields.Where(f => f.Name == fieldInfo.COLUMN_NAME).FirstOrDefault() != null)
        //    {
        //        return; // поле добавлять не надо
        //    }

        //    property.Fields.Add(factory.PropertyFactory.CreateField(fieldInfo));
        //}

        #endregion
    }
}