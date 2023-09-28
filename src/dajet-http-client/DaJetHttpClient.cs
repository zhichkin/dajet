using DaJet.Data;
using DaJet.Model;
using System.Collections;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Client
{
    public sealed class DaJetHttpClient : IAsyncDataSource
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly HttpClient _client;
        private readonly IDomainModel _domain;
        public DaJetHttpClient(IDomainModel domain, HttpClient client)
        {
            _domain = domain;
            _client = client;
        }
        public async Task CreateAsync(EntityObject entity)
        {
            string url = $"/home/{entity.TypeCode}";

            try
            {
                string json = JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);

                StringContent content = new(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _client.PostAsync(url, content);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    throw new InvalidOperationException();
                }
            }
            catch
            {
                throw;
            }
        }
        public async Task UpdateAsync(EntityObject entity)
        {
            string url = $"/home/{entity.TypeCode}";

            try
            {
                string json = JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);

                StringContent content = new(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _client.PutAsync(url, content);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException();
                }
            }
            catch
            {
                throw;
            }
        }
        public async Task DeleteAsync(Entity entity)
        {
            try
            {
                string url = $"/home/{entity.TypeCode}/{entity.Identity.ToString().ToLower()}";

                HttpResponseMessage response = await _client.DeleteAsync(url);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException();
                }
            }
            catch
            {
                throw;
            }
        }
        public async Task<IEnumerable> SelectAsync()
        {
            int typeCode = _domain.GetTypeCode(typeof(TreeNodeRecord));

            if (typeCode == 0)
            {
                throw new InvalidOperationException($"Type [{nameof(TreeNodeRecord)}] not found");
            }

            return await SelectAsync(typeCode, "parent", Entity.Undefined);
        }
        public async Task<EntityObject> SelectAsync(Entity primaryKey)
        {
            if (primaryKey.IsUndefined || primaryKey.IsEmpty)
            {
                return null;
            }

            Type type = _domain.GetEntityType(primaryKey.TypeCode);

            if (type is null)
            {
                throw new InvalidOperationException($"Entity {primaryKey} not found");
            }

            string url = $"/home/{primaryKey.TypeCode}/{primaryKey.Identity}";

            HttpResponseMessage response = await _client.GetAsync(url);

            object record = await response.Content.ReadFromJsonAsync(type);

            if (record is not EntityObject entity)
            {
                return null;
            }

            entity.MarkAsOriginal();

            return entity;
        }
        public async Task<IEnumerable> SelectAsync(int typeCode, string propertyName, Entity value)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (type is null)
            {
                throw new InvalidOperationException($"Entity [{typeCode}] not found");
            }

            string url = $"/home/{typeCode}/{propertyName}/{value}";

            HttpResponseMessage response = await _client.GetAsync(url);

            Type listType = typeof(List<>).MakeGenericType(type);

            object result = await response.Content.ReadFromJsonAsync(listType);

            if (result is not IEnumerable list)
            {
                return null;
            }

            foreach (object item in list)
            {
                if (item is EntityObject entity)
                {
                    entity.MarkAsOriginal();
                }
            }

            return list;
        }
    }
}