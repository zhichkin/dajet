using System;

namespace DaJet.Model
{
    public sealed class TreeNodeRecord : EntityObject
    {
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