using DaJet.Data;
using DaJet.Flow.Model;
using DaJet.Model;
using System.Collections;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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
        public async Task<IEnumerable> SelectAsync()
        {
            return await SelectAsync<TreeNodeRecord>("parent", Entity.Undefined);
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
        public async Task<IEnumerable<T>> SelectAsync<T>(string property, Entity value) where T : EntityObject
        {
            int code = _domain.GetTypeCode(typeof(T));

            if (code == 0)
            {
                throw new InvalidOperationException($"[{typeof(T)}] type code not found");
            }

            string url = $"/home/{code}/{property}/{value}";

            HttpResponseMessage response = await _client.GetAsync(url);

            IEnumerable<T> list = await response.Content.ReadFromJsonAsync<IEnumerable<T>>();

            foreach (T item in list)
            {
                item.MarkAsOriginal();
            }

            return list;
        }
        private async Task<IEnumerable> SelectAsync(int code, string property, Entity value)
        {
            Type type = _domain.GetEntityType(code);

            if (type is null)
            {
                throw new InvalidOperationException($"Entity [{code}] not found");
            }

            string url = $"/home/{code}/{property}/{value}";

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



        public async Task<IEnumerable<T>> SelectAsync<T>(Expression<Func<T, bool>> where) where T : EntityObject
        {
            Dictionary<string, object> parameters = new ExpressionWhereVisitor().GetParameters(where);

            foreach (var item in parameters)
            {
                
            }

            return new List<T>();
        }
    }

    internal class ExpressionWhereVisitor : ExpressionVisitor
    {
        private string _propertyName = string.Empty;
        private Dictionary<string, object> _parameters = new();

        public Dictionary<string, object> GetParameters(Expression expression)
        {
            Visit(expression); return _parameters;
        }
        protected override Expression VisitBinary(BinaryExpression expression)
        {
            if (expression.NodeType == ExpressionType.Equal)
            {
                if (expression.Left is MemberExpression property)
                {
                    _propertyName = property.Member.Name;

                    if (expression.Right is MemberExpression parameter)
                    {
                        string name = parameter.Member.Name;

                        if (parameter.Expression is ConstantExpression constant)
                        {
                            object value = constant.Value.GetType().GetField(name).GetValue(constant.Value);

                            _parameters.Add(_propertyName, value);
                        }
                    }
                    else
                    {
                        // Unexpected expression ?
                    }
                }
            }

            return base.VisitBinary(expression);
        }
    }
}

//if (!response.IsSuccessStatusCode)
//{
//    string result = await response.Content.ReadAsStringAsync();
 
//    ErrorText = response.ReasonPhrase
//        + (string.IsNullOrEmpty(result)
//        ? string.Empty
//        : Environment.NewLine + result);
//}