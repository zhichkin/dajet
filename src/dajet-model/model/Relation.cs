using DaJet.Data;

namespace DaJet.Model
{
    public sealed class Relation
    {
        public Entity Source { get; set; }
        public TypeDef Target { get; set; }
    }
}