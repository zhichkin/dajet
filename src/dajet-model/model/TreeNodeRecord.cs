using System;

namespace DaJet.Model
{
    [Entity(-10)]
    public sealed class TreeNodeRecord : EntityObject
    {
        private static readonly int MY_TYPE_CODE = -10;
        public TreeNodeRecord() : base(MY_TYPE_CODE) { }
        public TreeNodeRecord(Guid identity) : base(MY_TYPE_CODE, identity) { }
        public string Name { get; set; }
        public bool IsFolder { get; set; }
        public EntityObject Value { get; set; }
        public TreeNodeRecord Parent { get; set; }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}