using DaJet.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Server
{
    internal static class HttpRequestExtensions
    {
        internal static async Task<Dictionary<string, object>> GetParametersFromBody(this HttpRequest request)
        {
            if (request.ContentLength == 0)
            {
                return new Dictionary<string, object>();
            }

            JsonSerializerOptions options = new()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                Converters = { new DictionaryJsonConverter() }
            };

            return await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(request.Body, options);
        }
    }
}