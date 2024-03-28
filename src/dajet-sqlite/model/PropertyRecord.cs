using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Sqlite
{
    public sealed class PropertyRecord : EntityObject
    {
        private string _name = string.Empty;
        private Entity _owner = Entity.Undefined; // entity
        private bool _readonly; // database-generated value
        private string _column = string.Empty; // table column name
        private string _type = string.Empty; // property value type flags { l, n, t, s, r } [ b, u, v ]
        private int _code; // entity type code - zero is for any reference
        private int _size; // string or binary size in chars or bytes - zero is for varchar(max) or varbinary(max)
        private int _scale; // numeric digits after decimal - zero is for integer
        [JsonPropertyName(nameof(Name))] public string Name { get { return _name; } set { Set(value, ref _name); } }
        [JsonPropertyName(nameof(Owner))] public Entity Owner { get { return _owner; } set { Set(value, ref _owner); } }
        [JsonPropertyName(nameof(IsReadOnly))] public bool IsReadOnly { get { return _readonly; } set { Set(value, ref _readonly); } }
        [JsonPropertyName(nameof(Column))] public string Column { get { return _column; } set { Set(value, ref _column); } }
        [JsonPropertyName(nameof(Type))] public string Type { get { return _type; } set { Set(value, ref _type); } }
        [JsonPropertyName(nameof(Code))] public int Code { get { return _code; } set { Set(value, ref _code); } }
        [JsonPropertyName(nameof(Size))] public int Size { get { return _size; } set { Set(value, ref _size); } }
        [JsonPropertyName(nameof(Scale))] public int Scale { get { return _scale; } set { Set(value, ref _scale); } }
        public override string ToString() { return string.Format("{0} {1} ({2})", Owner, Name, Column); }
    }
}