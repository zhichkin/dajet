using DaJet.Data;
using DaJet.Json;
using DaJet.Model;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Client
{
    public sealed class DaJetHttpClient : IDataSource
    {
        private readonly HttpClient _client;
        private readonly IDomainModel _domain;
        public DaJetHttpClient(IDomainModel domain, HttpClient client)
        {
            _domain = domain;
            _client = client;
        }

        #region "IDataSource interface implementation"

        public void Create(Persistent entity)
        {
            throw new NotImplementedException();
        }
        public Task CreateAsync(Persistent entity)
        {
            throw new NotImplementedException();
        }
        public void Delete(Persistent entity)
        {
            throw new NotImplementedException();
        }
        public Task DeleteAsync(Persistent entity)
        {
            throw new NotImplementedException();
        }
        public List<EntityObject> Select(QueryObject query)
        {
            throw new NotImplementedException();
        }
        public Task<List<EntityObject>> SelectAsync(QueryObject query)
        {
            throw new NotImplementedException();
        }
        public void Update(Persistent entity)
        {
            throw new NotImplementedException();
        }
        public Task UpdateAsync(Persistent entity)
        {
            throw new NotImplementedException();
        }

        #endregion
        public void Select(Persistent entity)
        {
            if (Environment.OSVersion.Platform == PlatformID.Other)
            {
                // Any other operating system. This includes Browser (WASM).
            }
        }
        public async Task SelectAsync(Persistent entity)
        {
            throw new NotImplementedException();
        }
        public async Task<List<TreeNodeRecord>> SelectTreeNodes()
        {
            List<TreeNodeRecord> list = new();

            string url = $"/home";
            HttpResponseMessage response = await _client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();

                //ErrorText = response.ReasonPhrase
                //    + (string.IsNullOrEmpty(result)
                //    ? string.Empty
                //    : Environment.NewLine + result);
            }
            else
            {
                JsonSerializerOptions options = new()
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };

                options.Converters.Add(new EntityJsonConverter(_domain));

                list = await response.Content.ReadFromJsonAsync<List<TreeNodeRecord>>(options);
            }

            return list;
        }

        public async Task<Persistent> SelectAsync(Type type, Guid uuid)
        {
            string url = $"/home/{type.FullName}/{uuid}";
            HttpResponseMessage response = await _client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();

                throw new Exception(result);

                //ErrorText = response.ReasonPhrase
                //    + (string.IsNullOrEmpty(result)
                //    ? string.Empty
                //    : Environment.NewLine + result);
            }
            else
            {
                JsonSerializerOptions options = new()
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };

                options.Converters.Add(new EntityJsonConverter(_domain));

                return await response.Content.ReadFromJsonAsync<TreeNodeRecord>(options);
            }
        }
    }
}