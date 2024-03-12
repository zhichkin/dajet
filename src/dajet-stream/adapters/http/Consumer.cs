using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Stream.Http
{
    public sealed class Consumer : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly ConsumeStatement _options;
        private readonly Uri _uri;
        private readonly string _target;
        private readonly HttpClient _client = new();
        public Consumer(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ConsumeStatement statement)
            {
                throw new ArgumentException(nameof(ConsumeStatement));
            }

            StreamFactory.BindVariables(in _scope);

            _options = statement;

            _uri = _scope.GetUri(_options.Target);

            if (_options.Into?.Value is VariableReference variable)
            {
                _target = variable.Identifier;
            }

            if (!_scope.Variables.ContainsKey(_target))
            {
                _scope.Variables.Add(_target, new DataObject(2));
            }

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_target} is not found");
            }

            declare.Type.Binding = CreateResponseSchema();

            StreamFactory.MapOptions(in _scope);
            
            _next = StreamFactory.CreateStream(in scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose()
        {
            try
            {
                _client?.Dispose();
            }
            finally
            {
                _next?.Dispose();
            }
        }
        private List<ColumnExpression> CreateResponseSchema()
        {
            return new List<ColumnExpression>()
            {
                new()
                {
                    Alias = "Code",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Body",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                }
            };
        }
        private Dictionary<string, string> GetRequestHeaders()
        {
            Dictionary<string, string> headers = new();

            foreach (var option in _scope.Variables)
            {
                if (option.Key == _target || option.Key == "Method" || option.Key == "Body")
                {
                    continue; // http request method and body
                }

                if (StreamFactory.TryGetOption(in _scope, option.Key, out object value))
                {
                    headers.Add(option.Key, value.ToString());
                }
            }

            return headers;
        }
        private string GetRequestBody()
        {
            if (StreamFactory.TryGetOption(in _scope, "Body", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private HttpMethod GetHttpMethod()
        {
            if (StreamFactory.TryGetOption(in _scope, "Method", out object value))
            {
                if (value is string method)
                {
                    if (method == "POST") { return HttpMethod.Post; }
                    else if (method == "GET") { return HttpMethod.Get; }
                    else if (method == "PUT") { return HttpMethod.Put; }
                    else if (method == "DELETE") { return HttpMethod.Delete; }
                    else if (method == "HEAD") { return HttpMethod.Head; }
                    else if (method == "PATCH") { return HttpMethod.Patch; }
                    else if (method == "TRACE") { return HttpMethod.Trace; }
                    else if (method == "OPTIONS") { return HttpMethod.Options; }
                    else if (method == "CONNECT") { return HttpMethod.Connect; }
                }
            }

            return HttpMethod.Get;
        }
        public void Process()
        {
            Dictionary<string, string> headers = GetRequestHeaders();

            using (HttpRequestMessage request = new(GetHttpMethod(), _uri))
            {
                foreach (var header in headers)
                {
                    if (!header.Key.StartsWith("Content"))
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                request.Content = new StringContent(GetRequestBody(), Encoding.UTF8);

                request.Content.Headers.Clear();

                foreach (var header in headers)
                {
                    if (header.Key.StartsWith("Content"))
                    {
                        request.Content.Headers.Add(header.Key, header.Value);
                    }
                }

                HttpResponseMessage response = _client.Send(request);

                response.EnsureSuccessStatusCode();

                if (_scope.TryGetValue(_target, out object value))
                {
                    if (value is DataObject message)
                    {
                        string content = response.Content?.ReadAsStringAsync().Result;

                        message.SetValue("Code", ((int)response.StatusCode).ToString());
                        message.SetValue("Body", content is null ? string.Empty : content);
                    }
                }
            }

            _next?.Process();
        }
    }
}