using DaJet.Data;
using DaJet.Model;
using System.Collections;
using System.Net.Http.Json;

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
        public void Select(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public void Delete(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public void Update(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        #endregion

        public async Task<EntityObject> SelectAsync(Entity primaryKey)
        {
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
        public Task CreateAsync(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public Task UpdateAsync(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public Task DeleteAsync(EntityObject entity)
        {
            throw new NotImplementedException();
        }
    }
}