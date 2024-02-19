using DaJet.Data;

namespace DaJet.Scripting.Model
{
    public sealed class MemberAccessDescriptor
    {
        public MemberAccessDescriptor() { }
        public MemberAccessDescriptor(in string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        public string Value { get; set; } // scalar value
        public string Target { get; set; }
        public string Member { get; set; }
        public Type MemberType { get; set; } //NOTE: { Array | object | scalar type }
        public bool TryGetValue(in Dictionary<string, object> context, out object value)
        {
            value = Value;

            if (value is not null)
            {
                return true;
            }
            else if (context.TryGetValue(Target, out value))
            {
                if (string.IsNullOrEmpty(Member)) // script @parameter
                {
                    return true;
                }
                else if (value is DataObject record) // @variable.member value
                {
                    if (record.TryGetValue(Member, out value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}