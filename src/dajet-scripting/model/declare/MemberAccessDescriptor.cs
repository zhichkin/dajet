namespace DaJet.Scripting.Model
{
    public sealed class MemberAccessDescriptor
    {
        public string Target { get; set; }
        public string Member { get; set; }
        public Type MemberType { get; set; } //NOTE: { Array | object }
    }
}