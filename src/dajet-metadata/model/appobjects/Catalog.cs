using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class Catalog : ApplicationObject,
        IEntityCode, IEntityDescription, IEntityHierarchy, IPredefinedValueOwner, ITablePartOwner
    {
        public int CodeLength { get; set; } = 9;
        public CodeType CodeType { get; set; } = CodeType.String;
        public int DescriptionLength { get; set; } = 25;
        public bool IsHierarchical { get; set; } = false;
        public HierarchyType HierarchyType { get; set; } = HierarchyType.Groups;
        public List<TablePart> TableParts { get; set; } = new List<TablePart>();
        public List<PredefinedValue> PredefinedValues { get; set; } = new List<PredefinedValue>();
    }
    
    //PropertyNameLookup.Add("_idrref", "Ссылка");
    //PropertyNameLookup.Add("_version", "ВерсияДанных");
    //PropertyNameLookup.Add("_marked", "ПометкаУдаления");
    //PropertyNameLookup.Add("_predefinedid", "Предопределённый");
    //PropertyNameLookup.Add("_code", "Код"); // 1.17 - длина кода (0 - не используется), 1.18 - тип кода (0 - число, 1 - строка)
    //PropertyNameLookup.Add("_description", "Наименование"); // 1.19 - длина наименования (0 - не используется)
    //PropertyNameLookup.Add("_folder", "ЭтоГруппа");      // 1.36 (0 - иерархия групп и элементов, 1 - иерархия элементов)
    //PropertyNameLookup.Add("_parentidrref", "Родитель"); // 1.37 - иерархический (0 - нет, 1 - да)
    //PropertyNameLookup.Add("_owneridrref", "Владелец");   // 1.12.1 - количество владельцев справочника
    //PropertyNameLookup.Add("_ownerid_type", "Владелец");  // 1.12.2,     1.12.3,     1.12.N     - описание владельцев
    //PropertyNameLookup.Add("_ownerid_rtref", "Владелец"); // 1.12.2.2.1, 1.12.3.2.1, 1.12.N.2.1 - uuid'ы владельцев
    //PropertyNameLookup.Add("_ownerid_rrref", "Владелец");
    // Свойство "Владелец"       (1.12.1 == 0) не используется
    // _OwnerIDRRef binary(16)   (1.12.1 == 1)
    // _OwnerID_TYPE binary(1)   (1.12.1 > 1)
    // _OwnerID_RTRef binary(4)  (1.12.1 > 1)
    // _OwnerID_RRRef binary(16) (1.12.1 > 1)
}