using System;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class InfoBaseRecord
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Description))] public string Description { get; set; } = string.Empty;
        [JsonPropertyName(nameof(UseExtensions))] public bool UseExtensions { get; set; } = true;
        [JsonPropertyName(nameof(DatabaseProvider))] public string DatabaseProvider { get; set; } = string.Empty;
        [JsonPropertyName(nameof(ConnectionString))] public string ConnectionString { get; set; } = string.Empty;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }

        #region " Переопределение методов сравнения "
        public override int GetHashCode() { return Uuid.GetHashCode(); }
        public override bool Equals(object obj)
        {
            if (obj is null) { return false; }
            if (obj is not InfoBaseRecord test) { return false; }
            return Uuid == test.Uuid;
        }
        public static bool operator ==(InfoBaseRecord left, InfoBaseRecord right)
        {
            if (object.ReferenceEquals(left, right)) { return true; }
            if ((left is null) || (right is null)) { return false; }
            return left.Equals(right);
        }
        public static bool operator !=(InfoBaseRecord left, InfoBaseRecord right)
        {
            return !(left == right);
        }
        #endregion
    }
}