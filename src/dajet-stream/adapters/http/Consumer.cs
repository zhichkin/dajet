using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Net;
using System.Text;

namespace DaJet.Stream.Http
{
    public sealed class Consumer : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly ConsumeStatement _options;
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
                if (option.Key == _target ||
                    option.Key == "Method" ||
                    option.Key == "Body" ||
                    option.Key == "OnError")
                {
                    continue; // special processor properties
                }

                if (StreamFactory.TryGetOption(in _scope, option.Key, out object value))
                {
                    headers.Add(option.Key, value.ToString());
                }
            }

            return headers;
        }
        private string GetOnError()
        {
            if (StreamFactory.TryGetOption(in _scope, "OnError", out object value))
            {
                return value.ToString();
            }

            return "break";
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
            string content;
            HttpStatusCode code;

            bool throw_on_error = (GetOnError() == "break");

            try
            {
                code = ProcessRequest(out content);
            }
            catch (Exception error)
            {
                if (throw_on_error) { throw; }
                else
                {
                    code = HttpStatusCode.BadRequest;
                    content = ExceptionHelper.GetErrorMessage(error);
                }
            }
            
            ConfigureResponseObject(code, in content);

            _next?.Process();
        }
        private HttpStatusCode ProcessRequest(out string content)
        {
            Uri uri = _scope.GetUri(_options.Target);

            Dictionary<string, string> headers = GetRequestHeaders();

            using (HttpRequestMessage request = new(GetHttpMethod(), uri))
            {
                ConfigureRequestHeaders(in request, in headers);

                request.Content = new StringContent(GetRequestBody(), Encoding.UTF8);

                ConfigureContentHeaders(in request, in headers);

                HttpResponseMessage response = _client.Send(request);

                content = response.Content?.ReadAsStringAsync().Result ?? string.Empty;

                if (!response.IsSuccessStatusCode && string.IsNullOrEmpty(content))
                {
                    content = response.ReasonPhrase;
                }

                return response.StatusCode;
            }
        }
        private void ConfigureRequestHeaders(in HttpRequestMessage request, in Dictionary<string, string> headers)
        {
            request.Headers.Clear();

            foreach (var header in headers)
            {
                if (!header.Key.StartsWith("Content"))
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }
        }
        private void ConfigureContentHeaders(in HttpRequestMessage request, in Dictionary<string, string> headers)
        {
            request.Content.Headers.Clear();

            foreach (var header in headers)
            {
                if (header.Key.StartsWith("Content"))
                {
                    request.Content.Headers.Add(header.Key, header.Value);
                }
            }
        }
        private void ConfigureResponseObject(HttpStatusCode code, in string content)
        {
            if (_scope.TryGetValue(_target, out object value))
            {
                if (value is DataObject message)
                {
                    message.SetValue("Code", ((int)code).ToString());
                    message.SetValue("Body", content is null ? string.Empty : content);
                }
            }
        }
    }
}