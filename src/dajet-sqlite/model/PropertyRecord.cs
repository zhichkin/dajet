using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Sqlite
{
    public sealed class PropertyRecord : EntityObject
    {
        private Entity _owner = Entity.Undefined; // entity
        private string _name = string.Empty;
        private bool _readonly; // database-generated value
        private string _column = string.Empty; // database table column name
        private string _type = string.Empty; // property value type flags { l, n, t, s, r } [ b, u ]
        private int _length; // string or binary size in chars or bytes - zero is for varchar(max) or varbinary(max)
        private bool _fixed; // string or binary length qualifier [var]char or [var]binary
        private int _precision; // numeric precision (total digits) if is integer - size in bytes
        private int _scale; // numeric digits after decimal - zero is for integer
        private bool _signed; // integer type
        private int _discriminator; // entity type code - zero is for any reference type
        private int _primarykey; // ordinal position within primary key of the entity (owner)
        [JsonPropertyName(nameof(Owner))] public Entity Owner { get { return _owner; } set { Set(value, ref _owner); } }
        [JsonPropertyName(nameof(Name))] public string Name { get { return _name; } set { Set(value, ref _name); } }
        [JsonPropertyName(nameof(IsReadOnly))] public bool IsReadOnly { get { return _readonly; } set { Set(value, ref _readonly); } }
        [JsonPropertyName(nameof(Column))] public string Column { get { return _column; } set { Set(value, ref _column); } }
        [JsonPropertyName(nameof(Type))] public string Type { get { return _type; } set { Set(value, ref _type); } }
        [JsonPropertyName(nameof(Length))] public int Length { get { return _length; } set { Set(value, ref _length); } }
        [JsonPropertyName(nameof(IsFixed))] public bool IsFixed { get { return _fixed; } set { Set(value, ref _fixed); } }
        [JsonPropertyName(nameof(Precision))] public int Precision { get { return _precision; } set { Set(value, ref _precision); } }
        [JsonPropertyName(nameof(Scale))] public int Scale { get { return _scale; } set { Set(value, ref _scale); } }
        [JsonPropertyName(nameof(IsSigned))] public bool IsSigned { get { return _signed; } set { Set(value, ref _signed); } }
        [JsonPropertyName(nameof(Discriminator))] public int Discriminator { get { return _discriminator; } set { Set(value, ref _discriminator); } }
        [JsonPropertyName(nameof(PrimaryKey))] public int PrimaryKey { get { return _primarykey; } set { Set(value, ref _primarykey); } }
        public override string ToString() { return string.Format("{0} {1} ({2}) [{3}]", Owner, Name, Column, Type); }
    }
}