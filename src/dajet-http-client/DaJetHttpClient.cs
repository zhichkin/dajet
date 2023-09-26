using DaJet.Data;
using DaJet.Model;
using System.Net.Http.Json;
using System.Reflection;
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

        public void Create(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        
        public void Delete(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public Task DeleteAsync(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public List<EntityObject> Select(QueryObject query)
        {
            throw new NotImplementedException();
        }
        
        public void Update(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public Task UpdateAsync(EntityObject entity)
        {
            throw new NotImplementedException();
        }

        #endregion
        public void Select(EntityObject entity)
        {
            if (Environment.OSVersion.Platform == PlatformID.Other)
            {
                // Any other operating system. This includes Browser (WASM).
            }
        }
        public async Task SelectAsync(EntityObject entity)
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

                //options.Converters.Add(new EntityJsonConverter(_domain));

                list = await response.Content.ReadFromJsonAsync<List<TreeNodeRecord>>(options);

                foreach (TreeNodeRecord record in list)
                {
                    record.MarkAsOriginal();
                }
            }

            return list;
        }

        public async Task<EntityObject> SelectAsync(Type type, Guid uuid)
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

                //options.Converters.Add(new EntityJsonConverter(_domain));

                return await response.Content.ReadFromJsonAsync<TreeNodeRecord>(options);
            }
        }
        public async Task<EntityObject> SelectAsync(Entity entity)
        {
            Type type = _domain.GetEntityType(entity.TypeCode);

            if (type is null)
            {
                throw new InvalidOperationException($"Entity type [{entity.TypeCode}] not found");
            }

            string url = $"/home/{type.FullName}/{entity.Identity}";
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

                //options.Converters.Add(new EntityJsonConverter(_domain));

                TreeNodeRecord record = await response.Content.ReadFromJsonAsync<TreeNodeRecord>(options);

                record.MarkAsOriginal();

                return record;
            }
        }

        public Task CreateAsync(EntityObject entity)
        {
            throw new NotImplementedException();
        }

        public Task<List<EntityObject>> SelectAsync(QueryObject query)
        {
            throw new NotImplementedException();
        }
    }
}