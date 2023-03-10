using DaJet.Data;

namespace DaJet.Metadata.Data.Model
{
    public sealed class Relation
    {
        public Entity Source { get; set; }
        public TypeDef Target { get; set; }
    }
}