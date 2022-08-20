namespace DaJet.Data
{
    public readonly struct EntityRef
    {
        public static readonly EntityRef Empty = new();
        public static EntityRef Parse(string value)
        {
            string[] parts = value.TrimStart('{').TrimEnd('}').Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                throw new FormatException($"Failed to parse EntityRef value: {value}");
            }

            int typeCode = int.Parse(parts[0]);
            Guid identity = new Guid(parts[1]);

            return new EntityRef(typeCode, identity);
        }
        public EntityRef(int typeCode, Guid identity)
        {
            TypeCode = typeCode;
            Identity = identity;
        }
        public int TypeCode { get; } = 0;
        public Guid Identity { get; } = Guid.Empty;
        public override string ToString()
        {
            return $"{{{TypeCode}:{Identity}}}";
        }

        #region " Переопределение методов сравнения "

        public override int GetHashCode()
        {
            return Identity.GetHashCode();
        }
        public override bool Equals(object? obj)
        {
            if (obj == null) { return false; }

            if (obj is not EntityRef test)
            {
                return false;
            }

            return (this == test);
        }
        public static bool operator ==(EntityRef left, EntityRef right)
        {
            return left.TypeCode == right.TypeCode
                && left.Identity == right.Identity;
        }
        public static bool operator !=(EntityRef left, EntityRef right)
        {
            return !(left == right);
        }

        #endregion
    }
}