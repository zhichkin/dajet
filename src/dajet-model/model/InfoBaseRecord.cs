using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class InfoBaseRecord : EntityObject
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private bool _use_extensions = false;
        private string _provider = string.Empty;
        private string _connection = string.Empty;
        [JsonPropertyName(nameof(Name))] public string Name { get { return _name; } set { Set(value, ref _name); } }
        [JsonPropertyName(nameof(Description))] public string Description { get { return _description; } set { Set(value, ref _description); } }
        [JsonPropertyName(nameof(UseExtensions))] public bool UseExtensions { get { return _use_extensions; } set { Set(value, ref _use_extensions); } }
        [JsonPropertyName(nameof(DatabaseProvider))] public string DatabaseProvider { get { return _provider; } set { Set(value, ref _provider); } }
        [JsonPropertyName(nameof(ConnectionString))] public string ConnectionString { get { return _connection; } set { Set(value, ref _connection); } }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}