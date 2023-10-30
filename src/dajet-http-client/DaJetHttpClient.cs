using DaJet.Data;
using DaJet.Flow.Model;
using DaJet.Json;
using DaJet.Model;
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
        static DaJetHttpClient()
        {
            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
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

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    entity.MarkAsOriginal();
                }
                else
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

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    entity.MarkAsOriginal();
                }
                else
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
        public async Task<T> SelectAsync<T>(Guid identity) where T : EntityObject
        {
            Entity entity = _domain.GetEntity<T>(identity);

            return await SelectAsync(entity) as T;
        }
        public async Task<T> SelectAsync<T>(Entity entity) where T : EntityObject
        {
            return await SelectAsync(entity) as T;
        }
        public async Task<EntityObject> SelectAsync(Entity entity)
        {
            if (entity.IsUndefined || entity.IsEmpty)
            {
                return null;
            }

            Type type = _domain.GetEntityType(entity.TypeCode);

            if (type is null)
            {
                throw new InvalidOperationException($"Entity {entity} not found");
            }

            string url = $"/home/{entity.TypeCode}/{entity.Identity}";

            HttpResponseMessage response = await _client.GetAsync(url);

            object record = await response.Content.ReadFromJsonAsync(type);

            if (record is not EntityObject result)
            {
                return null;
            }

            result.MarkAsOriginal();

            return result;
        }
        public async Task<IEnumerable<T>> QueryAsync<T>() where T : EntityObject
        {
            int typeCode = _domain.GetTypeCode(typeof(T));

            if (typeCode == 0)
            {
                throw new InvalidOperationException($"[{typeof(T)}] type code not found");
            }

            string url = $"/home/{typeCode}";

            HttpResponseMessage response = await _client.GetAsync(url);

            IEnumerable<T> list = await response.Content.ReadFromJsonAsync<IEnumerable<T>>();

            foreach (T item in list)
            {
                item.MarkAsOriginal();
            }

            return list;
        }
        public async Task<IEnumerable<T>> QueryAsync<T>(Entity owner) where T : EntityObject
        {
            int typeCode = _domain.GetTypeCode(typeof(T));

            if (typeCode == 0)
            {
                throw new InvalidOperationException($"[{typeof(T)}] type code not found");
            }

            string url = $"/home/{typeCode}/{owner.TypeCode}/{owner.Identity}";

            HttpResponseMessage response = await _client.GetAsync(url);

            IEnumerable<T> list = await response.Content.ReadFromJsonAsync<IEnumerable<T>>();

            foreach (T item in list)
            {
                item.MarkAsOriginal();
            }

            return list;
        }
        
        private async Task<T> ExecuteScalar<T>(string command, Dictionary<string, object> parameters)
        {
            string url = $"/home/execute/{command}";

            HttpResponseMessage response = await _client.PostAsJsonAsync(url, parameters, JsonOptions);

            T result = await response.Content.ReadFromJsonAsync<T>();

            if (result is EntityObject entity)
            {
                entity.MarkAsOriginal();
            }

            return result;
        }

        public async Task<PipelineInfo> GetPipelineInfo(Guid uuid)
        {
            PipelineInfo info;

            string url = $"/home/get-pipeline-info/{uuid.ToString().ToLower()}";

            try
            {
                HttpResponseMessage response = await _client.GetAsync(url);

                info = await response.Content.ReadFromJsonAsync<PipelineInfo>();
            }
            catch
            {
                throw;
            }

            return info;
        }
        public async Task<List<PipelineInfo>> GetPipelineInfo()
        {
            List<PipelineInfo> list;

            try
            {
                HttpResponseMessage response = await _client.GetAsync("/flow");

                list = await response.Content.ReadFromJsonAsync<List<PipelineInfo>>();
            }
            catch
            {
                throw;
            }

            return list;
        }
        public async Task ExecutePipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await _client.PutAsync($"/flow/execute/{uuid.ToString().ToLower()}", null);
            }
            catch
            {
                throw;
            }
        }
        public async Task DisposePipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await _client.PutAsync($"/flow/dispose/{uuid.ToString().ToLower()}", null);
            }
            catch
            {
                throw;
            }
        }
        public async Task ReStartPipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await _client.PutAsync($"/flow/restart/{uuid.ToString().ToLower()}", null);

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    throw new Exception(result);
                }
            }
            catch
            {
                throw;
            }
        }
        public async Task<bool> ValidatePipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync($"/flow/validate/{uuid.ToString().ToLower()}");

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                string result = await response.Content.ReadAsStringAsync();

                throw new Exception(result);
            }
            catch
            {
                throw;
            }
        }

        public async Task<string> GetTreeNodeFullName(TreeNodeRecord node)
        {
            string url = $"/home/get-tree-node-full-name/{node.Identity.ToString().ToLower()}";

            HttpResponseMessage response = await _client.GetAsync(url);

            string name = await response.Content.ReadAsStringAsync();

            return name;
        }
        public async Task<List<ProcessorInfo>> GetAvailableProcessors()
        {
            HttpResponseMessage response = await _client.GetAsync($"/home/get-available-processors");

            var list = await response.Content.ReadFromJsonAsync<List<ProcessorInfo>>();

            return list;
        }
    }
}