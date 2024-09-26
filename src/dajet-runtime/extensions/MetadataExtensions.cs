using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;

namespace DaJet.Runtime
{
    public static class MetadataExtensions
    {
        public static DataObject ToDataObject(this MetadataObject metadata)
        {
            DataObject @object = new(7);

            string typeName = MetadataTypes.ResolveNameRu(metadata.GetType());

            if (metadata is TablePart table)
            {
                typeName = "ТабличнаяЧасть";
            }

            @object.SetValue("Ссылка", metadata.Uuid);
            @object.SetValue("Код", 0);
            @object.SetValue("Тип", typeName);
            @object.SetValue("Имя", metadata.Name);
            @object.SetValue("ПолноеИмя", $"{typeName}.{metadata.Name}");
            @object.SetValue("Таблица", string.Empty);
            @object.SetValue("Владелец", Guid.Empty);

            if (metadata is ApplicationObject entity)
            {
                @object.SetValue("Код", entity.TypeCode);
                @object.SetValue("Таблица", entity.TableName);
            }

            return @object;
        }
    }
}