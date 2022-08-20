using DaJet.Metadata.Model;
using System.Collections.Generic;

namespace DaJet.Metadata.Services
{
    public enum DiffType
    {
        ///<summary>No difference (used just as a root of child differences)</summary>
        None,
        ///<summary>New item to add</summary>
        Insert,
        ///<summary>Some properties has been changed</summary>
        Update,
        ///<summary>Target item to be deleted</summary>
        Delete
    }
    public sealed class MetaDiff
    {
        public MetaDiff() { }
        public MetaDiff(MetaDiff parent, MetadataObject target, DiffType difference)
        {
            Parent = parent;
            Target = target;
            Difference = difference;
        }
        public MetaDiff Parent { set; get; }
        public DiffType Difference { set; get; }
        public MetadataObject Target { set; get; }
        public List<MetaDiff> Children { get; } = new List<MetaDiff>();
        public Dictionary<string, object> NewValues { get; } = new Dictionary<string, object>();
    }
}