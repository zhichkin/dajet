namespace DaJet.Data
{
    public readonly struct Entity
    {
        public static readonly Entity Empty = new();
        public static Entity Parse(string value)
        {
            string[] parts = value.TrimStart('{').TrimEnd('}').Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                throw new FormatException($"Failed to parse Entity value: {value}");
            }

            int typeCode = int.Parse(parts[0]);
            Guid identity = new Guid(parts[1]);

            return new Entity(typeCode, identity);
        }
        public static bool TryParse(string value, out Entity entity)
        {
            entity = Entity.Empty;

            string[] parts = value.TrimStart('{').TrimEnd('}').Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return false;
            }

            try
            {
                int typeCode = int.Parse(parts[0]);
                Guid identity = new Guid(parts[1]);
                entity = new Entity(typeCode, identity);
            }
            catch
            {
                return false;
            }

            return true;
        }
        public Entity(int typeCode, Guid identity)
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

            if (obj is not Entity test)
            {
                return false;
            }

            return (this == test);
        }
        public static bool operator ==(Entity left, Entity right)
        {
            return left.TypeCode == right.TypeCode
                && left.Identity == right.Identity;
        }
        public static bool operator !=(Entity left, Entity right)
        {
            return !(left == right);
        }

        #endregion
    }
}