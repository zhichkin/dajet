using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class ScriptRecord
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Owner))] public Guid Owner { get; set; } = Guid.Empty; // database
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(IsFolder))] public bool IsFolder { get; set; } = true;
        [JsonPropertyName(nameof(Parent))] public Guid Parent { get; set; } = Guid.Empty; // script folder
        [JsonPropertyName(nameof(Script))] public string Script { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Children))] public List<ScriptRecord> Children { get; set; } = new();

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }

        #region " Переопределение методов сравнения "
        public override int GetHashCode() { return Uuid.GetHashCode(); }
        public override bool Equals(object obj)
        {
            if (obj is null) { return false; }
            if (obj is not ScriptRecord test) { return false; }
            return Uuid == test.Uuid;
        }
        public static bool operator ==(ScriptRecord left, ScriptRecord right)
        {
            if (object.ReferenceEquals(left, right)) { return true; }
            if ((left is null) || (right is null)) { return false; }
            return left.Equals(right);
        }
        public static bool operator !=(ScriptRecord left, ScriptRecord right)
        {
            return !(left == right);
        }
        #endregion
    }
}