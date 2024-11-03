using System.Globalization;

namespace DaJet.Runtime.RabbitMQ
{
    internal static class HeaderSerializer
    {
        internal static string Serialize(in object value)
        {
            if (value is null) { return "null"; }
            else if (value is string text) { return text; }
            else if (value is bool boolean) { return boolean ? "true" : "false"; }
            else if (value is Guid uuid) { return uuid.ToString().ToLowerInvariant(); }
            else if (value is DateTime dateTime) { return dateTime.ToString("yyyy-MM-ddTHH:mm:ss"); }
            else if (value is byte[] binary) { return Convert.ToBase64String(binary); }
            else if (value is int int4) { return int4.ToString(CultureInfo.InvariantCulture); }
            else if (value is long int8) { return int8.ToString(CultureInfo.InvariantCulture); }
            else if (value is decimal dec8) { return dec8.ToString(CultureInfo.InvariantCulture); }
            else if (value is Entity entity) { return entity.ToString(); }
            else
            {
                return value.ToString();
            }
        }
        internal static object Deserialize(in string value)
        {
            if (value == "null") { return null; }
            else if (value == "true") { return true; }
            else if (value == "false") { return false; }
            else if (string.IsNullOrWhiteSpace(value)) { return value; }
            else if (int.TryParse(value, CultureInfo.InvariantCulture, out int int4)) { return int4; }
            else if (long.TryParse(value, CultureInfo.InvariantCulture, out long int8)) { return int8; }
            else if (Guid.TryParse(value, CultureInfo.InvariantCulture, out Guid uuid)) { return uuid; }
            else if (value.StartsWith('{') && Entity.TryParse(value, out Entity entity)) { return entity; }
            else if (value.Length >= 10 && DateTime.TryParse(value, out DateTime dateTime)) { return dateTime; }
            else
            {
                return value;
            }

            //else if (decimal.TryParse(value, CultureInfo.InvariantCulture, out decimal dec8)) { return dec8; }
        }
    }
}