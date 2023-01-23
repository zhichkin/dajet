using System.Text.Json.Serialization;

namespace DaJet.Studio.Model
{
    public sealed class InfoBaseModel
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Description))] public string Description { get; set; } = string.Empty;
        [JsonPropertyName(nameof(UseExtensions))] public bool UseExtensions { get; set; } = false;
        [JsonPropertyName(nameof(ConnectionString))] public string ConnectionString { get; set; } = string.Empty;
        [JsonPropertyName(nameof(DatabaseProvider))] public string DatabaseProvider { get; set; } = "SqlServer"; // { "SqlServer", "PostgreSql" }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }

        #region " Переопределение методов сравнения "
        public override int GetHashCode() { return Uuid.GetHashCode(); }
        public override bool Equals(object obj)
        {
            if (obj is null) { return false; }
            if (obj is not InfoBaseModel test) { return false; }
            return Uuid == test.Uuid;
        }
        public static bool operator ==(InfoBaseModel left, InfoBaseModel right)
        {
            if (object.ReferenceEquals(left, right)) { return true; }
            if ((left is null) || (right is null)) { return false; }
            return left.Equals(right);
        }
        public static bool operator !=(InfoBaseModel left, InfoBaseModel right)
        {
            return !(left == right);
        }
        #endregion
    }
}