﻿using DaJet.Data;
using DaJet.Http.Model;
using DaJet.Json;
using DaJet.Model;
using DaJet.Model.Http;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
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
            JsonOptions.Converters.Add(new DataObjectJsonConverter());
            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        private readonly HttpClient _client;
        private readonly IDomainModel _domain;
        public DaJetHttpClient(IDomainModel domain, HttpClient client)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }
        public IDomainModel Model { get { return _domain; } }

        #region "DAJET ODATA INTERFACE"
        public async Task CreateAsync(EntityObject entity)
        {
            string url = $"/data/{entity.TypeCode}";

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
            string url = $"/data/{entity.TypeCode}";

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
                string url = $"/data/{entity.TypeCode}/{entity.Identity.ToString().ToLower()}";

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
        public async Task<T> SelectAsync<T>(string name) where T : EntityObject
        {
            int typeCode = _domain.GetTypeCode(typeof(T));

            if (typeCode == 0)
            {
                throw new InvalidOperationException($"Type code of type [{typeof(T)}] is not found.");
            }

            string url = $"/data/{typeCode}/{name}";

            HttpResponseMessage response = await _client.GetAsync(url);

            object record = await response.Content.ReadFromJsonAsync<T>();

            if (record is not T result)
            {
                return null;
            }

            result.MarkAsOriginal();

            return result;
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

            string url = $"/data/{entity.TypeCode}/{entity.Identity}";

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

            string url = $"/data/{typeCode}";

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

            string url = $"/data/{typeCode}/{owner.TypeCode}/{owner.Identity}";

            HttpResponseMessage response = await _client.GetAsync(url);

            IEnumerable<T> list = await response.Content.ReadFromJsonAsync<IEnumerable<T>>();

            foreach (T item in list)
            {
                item.MarkAsOriginal();
            }

            return list;
        }
        public async Task<List<DataObject>> QueryAsync(string query, Dictionary<string, object> parameters)
        {
            string url = $"/data/query";

            parameters.Add("Query", query);

            HttpResponseMessage response = await _client.PostAsJsonAsync(url, parameters, JsonOptions);

            List<DataObject> result = await response.Content.ReadFromJsonAsync<List<DataObject>>(JsonOptions);

            return result;
        }
        #endregion

        #region "DAJET FLOW API"
        public async Task<List<PipelineInfo>> GetPipelineInfo()
        {
            List<PipelineInfo> list;

            try
            {
                HttpResponseMessage response = await _client.GetAsync("/flow/monitor");

                list = await response.Content.ReadFromJsonAsync<List<PipelineInfo>>();
            }
            catch
            {
                throw;
            }

            return list;
        }
        public async Task<PipelineInfo> GetPipelineInfo(Guid uuid)
        {
            PipelineInfo info;

            string url = $"/flow/monitor/{uuid.ToString().ToLower()}";

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

        public async Task DeletePipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await _client.DeleteAsync($"/flow/{uuid.ToString().ToLower()}");
            }
            catch
            {
                throw;
            }
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
            string url = $"/data/get-tree-node-full-name/{node.Identity.ToString().ToLower()}";

            HttpResponseMessage response = await _client.GetAsync(url);

            string name = await response.Content.ReadAsStringAsync();

            return name;
        }
        public async Task<List<HandlerModel>> GetAvailableHandlers()
        {
            HttpResponseMessage response = await _client.GetAsync($"/flow/handlers");

            var list = await response.Content.ReadFromJsonAsync<List<HandlerModel>>();

            return list;
        }
        public async Task<List<OptionModel>> GetAvailableOptions(string ownerTypeName)
        {
            HttpResponseMessage response = await _client.GetAsync($"/flow/options/{ownerTypeName}");

            var list = await response.Content.ReadFromJsonAsync<List<OptionModel>>();

            return list;
        }
        #endregion

        #region "DEPRICATED CODE EDITOR"
        public async Task<string> GetScriptUrl(Guid uuid)
        {
            string url = $"/api/url/{uuid}";

            HttpResponseMessage response = await _client.GetAsync(url);

            string result = await response.Content.ReadAsStringAsync();

            return result;
        }
        public async Task<QueryResponse> ExecuteNonQuery(ScriptRecord script)
        {
            QueryResponse result = new();

            try
            {
                InfoBaseRecord database = await SelectAsync<InfoBaseRecord>(script.Owner);

                QueryRequest request = new()
                {
                    DbName = database.Name,
                    Script = script.Script
                };

                HttpResponseMessage response = await _client.PostAsJsonAsync("/query/ddl", request);

                if (!response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    result.Success = false;
                    result.Error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(content)
                        ? string.Empty
                        : Environment.NewLine + content);
                }
                else
                {
                    result = await response.Content.ReadFromJsonAsync<QueryResponse>();
                }
            }
            catch (Exception error)
            {
                result.Success = false;
                result.Error = error.Message;
            }

            return result;
        }
        public async Task<QueryResponse> ExecuteScriptSql(ScriptRecord script)
        {
            QueryResponse result = new();

            try
            {
                InfoBaseRecord database = await SelectAsync<InfoBaseRecord>(script.Owner);

                QueryRequest request = new()
                {
                    DbName = database.Name,
                    Script = script.Script
                };

                HttpResponseMessage response = await _client.PostAsJsonAsync("/query/prepare", request);

                if (!response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    result.Success = false;
                    result.Error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(content)
                        ? string.Empty
                        : Environment.NewLine + content);
                }
                else
                {
                    result = await response.Content.ReadFromJsonAsync<QueryResponse>();
                }
            }
            catch (Exception error)
            {
                result.Success = false;
                result.Error = error.Message;
            }

            return result;
        }
        public async Task<QueryResponse> ExecuteScriptJson(ScriptRecord script)
        {
            QueryResponse result = new();

            try
            {
                string url = await GetScriptUrl(script.Identity);

                // do not override parameters defined in script
                Dictionary<string, object> parameters = new();

                HttpResponseMessage response = await _client.PostAsJsonAsync(url, parameters);

                string content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    result.Success = true;
                    result.Script = content;
                }
                else
                {
                    result.Success = false;
                    result.Error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(content)
                        ? string.Empty
                        : Environment.NewLine + content);
                }
            }
            catch (Exception error)
            {
                result.Success = false;
                result.Error = error.Message;
            }

            return result;
        }
        public async Task<QueryResponse> ExecuteScriptTable(ScriptRecord script)
        {
            QueryResponse result = new();

            try
            {
                string url = await GetScriptUrl(script.Identity);

                // do not override parameters defined in script
                Dictionary<string, object> parameters = new();

                HttpResponseMessage response = await _client.PostAsJsonAsync(url, parameters);

                if (!response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    result.Success = false;
                    result.Error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(content)
                        ? string.Empty
                        : Environment.NewLine + content);
                }
                else
                {
                    result.Success = true;
                    result.Result = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
                }
            }
            catch (Exception error)
            {
                result.Success = false;
                result.Error = error.Message;
            }

            return result;
        }
        #endregion

        #region "DAJET SCRIPT SERVICES"

        private const string URL_DAJET_LOG = "/dajet/log";
        private const string URL_DAJET_DIR = "/dajet/dir";
        private const string URL_DAJET_SRC = "/dajet/src";
        private const string URL_DAJET_EXE = "/dajet/exe";
        public async Task<string> GetServerLog()
        {
            string url = URL_DAJET_LOG;

            HttpResponseMessage response = await _client.GetAsync(url);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<List<CodeItem>> GetFolderItems(string path)
        {
            string url = URL_DAJET_DIR + path;

            HttpResponseMessage response = await _client.GetAsync(url);

            return await response.Content.ReadFromJsonAsync<List<CodeItem>>();
        }
        public async Task<string> CreateScriptFolder(string path)
        {
            string url = URL_DAJET_DIR + path;

            HttpResponseMessage response = await _client.PostAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        public async Task<string> DeleteScriptFolder(string path)
        {
            string url = URL_DAJET_DIR + path;

            HttpResponseMessage response = await _client.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        public async Task<string> RenameScriptFolder(string path, string name)
        {
            string url = URL_DAJET_DIR + path;

            HttpResponseMessage response = await _client.PutAsync(url, new StringContent(name));

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        public async Task<string> MoveScriptFolder(string path, string target)
        {
            string url = URL_DAJET_DIR + path;

            HttpResponseMessage response = await _client.PatchAsync(url, new StringContent(target));

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }

        public async Task<string> GetSourceCode(string path)
        {
            string url = URL_DAJET_SRC + path;

            HttpResponseMessage response = await _client.GetAsync(url);

            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> SaveSourceCode(string path, string code)
        {
            string url = URL_DAJET_SRC + path;

            HttpResponseMessage response = await _client.PostAsync(url, new StringContent(code));

            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> CreateScriptFile(string path)
        {
            string url = URL_DAJET_SRC + path;

            HttpResponseMessage response = await _client.PostAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        public async Task<string> DeleteScriptFile(string path)
        {
            string url = URL_DAJET_SRC + path;

            HttpResponseMessage response = await _client.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        public async Task<string> RenameScriptFile(string path, string name)
        {
            string url = URL_DAJET_SRC + path;

            HttpResponseMessage response = await _client.PutAsync(url, new StringContent(name));

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }
        public async Task<string> MoveScriptFile(string path, string target)
        {
            string url = URL_DAJET_SRC + path;

            HttpResponseMessage response = await _client.PatchAsync(url, new StringContent(target));

            if (response.IsSuccessStatusCode)
            {
                return string.Empty;
            }
            else
            {
                return response.ReasonPhrase;
            }
        }

        public async Task<object> ExecuteScript(string path, string code)
        {
            string url = URL_DAJET_EXE + path;

            HttpResponseMessage response = await _client.PostAsync(url, null);

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"{response.ReasonPhrase}{Environment.NewLine}{content}";
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }
            else if (content.StartsWith('{'))
            {
                return await response.Content.ReadFromJsonAsync<DataObject>(JsonOptions);
            }
            else if (content.StartsWith('['))
            {
                return await response.Content.ReadFromJsonAsync<List<DataObject>>(JsonOptions);
            }
            else
            {
                return content;
            }
        }
        #endregion

        #region "DATABASE VIEW GENERATOR API"
        public async Task<CreateDbViewsResponse> CreateDbViews(string infobase, string schema)
        {
            string url = $"/db/view/{infobase}";
            CreateDbViewsRequest options = new() { Schema = schema };
            HttpResponseMessage response = await _client.PostAsJsonAsync(url, options);
            return await response.Content.ReadFromJsonAsync<CreateDbViewsResponse>();
        }
        public async Task<string> DeleteDbViews(string infobase, string schema)
        {
            string url = $"/db/view/{infobase}?schema={schema}";
            HttpResponseMessage response = await _client.DeleteAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        //public async Task<string> ScriptDbViews(string infobase, string schema)
        //{
        //    string url = $"/db/view/{infobase}?schema={schema}";
        //    HttpResponseMessage response = await _client.GetAsync(url);
        //    return await response.Content.ReadAsStringAsync();
        //}
        #endregion

        public async Task<List<ExtensionModel>> GetExtensions(string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url);
            return await response.Content.ReadFromJsonAsync<List<ExtensionModel>>();
        }
        public async Task<List<MetadataItemModel>> GetMetadataItems(string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url);
            return await response.Content.ReadFromJsonAsync<List<MetadataItemModel>>();
        }
        public async Task<EnumModel> GetEnumObject(string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url);
            return await response.Content.ReadFromJsonAsync<EnumModel>();
        }
        public async Task<EntityModel> GetEntityObject(string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url);
            return await response.Content.ReadFromJsonAsync<EntityModel>();
        }
        public async Task<EntityModel> GetMetadataObject(string url)
        {
            HttpResponseMessage response = await _client.GetAsync($"{url}?details=full");

            EntityModel metadata = await response.Content.ReadFromJsonAsync<EntityModel>();

            return metadata;
        }

        public async Task<QueryResponse> ClearInfoBaseMetadataCache(string name)
        {
            HttpResponseMessage response = await _client.GetAsync($"/md/reset/{name}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }

            if (response.IsSuccessStatusCode)
            {
                return new QueryResponse()
                {
                    Success = true,
                    Message = $"Кэш базы данных [{name}] обновлён успешно."
                };
            }
            else
            {
                string message = string.Empty;

                if (!string.IsNullOrWhiteSpace(response.ReasonPhrase))
                {
                    message = $"[{response.ReasonPhrase}]";
                }

                string content = string.Empty;

                try
                {
                    content = await response.Content.ReadAsStringAsync();
                }
                catch (Exception error)
                {
                    content = ExceptionHelper.GetErrorMessage(error);
                }

                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = content;
                    }
                    else
                    {
                        message += " " + content;
                    }
                }

                return new QueryResponse()
                {
                    Success = false,
                    Message = message
                };
            }
        }
        public async Task<string> CompareMetadataAndDatabaseSchema(string infobase)
        {
            string url = $"/md/diagnostic/{infobase}";
            HttpResponseMessage response = await _client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
    }
}