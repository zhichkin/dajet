using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet.Metadata.Model
{
    public sealed class Account : ApplicationObject,
        IEntityCode, IEntityDescription, IEntityHierarchy, IPredefinedValueOwner, ITablePartOwner
    {
        public int CodeLength { get; set; } = 9;
        public string CodeMask { get; set; } = string.Empty;
        public CodeType CodeType { get; set; } = CodeType.String;
        public int DescriptionLength { get; set; } = 25;
        public bool IsHierarchical { get; set; } = true;
        public HierarchyType HierarchyType { get; set; } = HierarchyType.Elements;
        public bool UseAutoOrder { get; set; } = false;
        public int AutoOrderLength { get; set; } = 0;
        public Guid DimensionTypes { get; set; } = Guid.Empty; // Идентификатор плана видов характеристик
        public int MaxDimensionCount { get; set; } = 0; // Максимальное количество субконто
        public List<TablePart> TableParts { get; set; } = new();
        public List<PredefinedValue> PredefinedValues { get; set; } = new();
    }
}