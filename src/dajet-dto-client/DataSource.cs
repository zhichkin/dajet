using DaJet.Model;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace DaJet.Dto.Client
{
    public sealed class DataSource : IDataSource
    {
        private readonly HttpClient _http;
        private readonly IDomainModel _domain;
        private readonly DataSourceOptions _options;
        public DataSource(DataSourceOptions options, IDomainModel domain, HttpClient http)
        {
            _http = http;
            _domain = domain;
            _options = options;
        }
        public void Create(IPersistent entity)
        {
            HttpResponseMessage response = _http.PostAsJsonAsync(_options.ConnectionString, entity).Result;

            if (!response.IsSuccessStatusCode)
            {
                string result = response.Content?.ReadAsStringAsync().Result;

                string error = response.ReasonPhrase
                    + (string.IsNullOrEmpty(result)
                    ? string.Empty
                    : Environment.NewLine + result);

                throw new Exception(error);
            }
        }
        public void Select(IPersistent entity)
        {
            throw new NotImplementedException();
        }
        public void Update(IPersistent entity)
        {
            throw new NotImplementedException();
        }
        public void Delete(IPersistent entity)
        {
            throw new NotImplementedException();
        }
        public List<EntityObject> Select(QueryObject query)
        {
            string json = JsonSerializer.Serialize(query);

            HttpRequestMessage request = new(HttpMethod.Get, _options.ConnectionString)
            {
                Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json)
            };

            HttpResponseMessage response = _http.Send(request);

            List<EntityObject> list = response.Content.ReadFromJsonAsync<List<EntityObject>>().Result;

            if (response.IsSuccessStatusCode)
            {

            }

            return list;
        }
        public async Task<List<EntityObject>> SelectAsync(QueryObject query)
        {
            string json = JsonSerializer.Serialize(query);

            StringContent content = new(json, Encoding.UTF8, MediaTypeNames.Application.Json);

            HttpResponseMessage response = await _http.PostAsync(_options.ConnectionString + "/select", content);

            List<EntityObject> list = await response.Content.ReadFromJsonAsync<List<EntityObject>>();

            if (response.IsSuccessStatusCode)
            {

            }

            return list;
        }
    }
}