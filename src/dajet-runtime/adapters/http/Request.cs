using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Net;
using System.Text;

namespace DaJet.Runtime.Http
{
    public sealed class Request : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly HttpClient _client = new(new SocketsHttpHandler()
        {
            MaxConnectionsPerServer = 1 //THINK: implement "circuit breaker" to prevent port exhaustion
        });
        private readonly string _target;
        private readonly RequestStatement _statement;
        public Request(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not RequestStatement statement)
            {
                throw new ArgumentException(nameof(RequestStatement));
            }

            StreamFactory.BindVariables(in _scope);

            _statement = statement;

            if (_statement.Response is not VariableReference variable)
            {
                throw new InvalidOperationException($"Response variable is not defined");
            }

            _target = variable.Identifier;

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_target} is not found");
            }

            declare.Type.Binding = CreateResponseSchema(); //NOTE: used by processors down the stream
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
                    Alias = "Value",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                }
            };
        }
        private Dictionary<string, string> GetRequestHeaders()
        {
            Dictionary<string, string> headers = new(_statement.Headers.Count);

            foreach (ColumnExpression accessor in _statement.Headers)
            {
                if (StreamFactory.TryEvaluate(in _scope, accessor.Expression, out object value))
                {
                    headers.Add(accessor.Alias, value.ToString());
                }
            }

            return headers;
        }
        private SyntaxNode GetOptionAccessor(in string name)
        {
            for (int i = 0; i < _statement.Options.Count; i++)
            {
                ColumnExpression accessor = _statement.Options[i];

                if (accessor.Alias == name)
                {
                    return accessor.Expression;
                }
            }

            return null;
        }
        private bool WhenIsTrue()
        {
            if (_statement.When is null) { return true; }

            SyntaxNode expression = _statement.When;

            return StreamFactory.Evaluate(in _scope, in expression);
        }
        private string GetOnError()
        {
            SyntaxNode accessor = GetOptionAccessor("OnError");

            if (StreamFactory.TryEvaluate(in _scope, in accessor, out object value))
            {
                return value.ToString();
            }
            
            return "break";
        }
        private HttpMethod GetHttpMethod()
        {
            SyntaxNode accessor = GetOptionAccessor("Method");

            if (StreamFactory.TryEvaluate(in _scope, in accessor, out object value))
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

            return HttpMethod.Post;
        }
        private string GetRequestContent()
        {
            SyntaxNode accessor = GetOptionAccessor("Content");

            if (StreamFactory.TryEvaluate(in _scope, in accessor, out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        
        public void Process()
        {
            if (WhenIsTrue())
            {
                Execute();
            }
            
            _next?.Process();
        }
        private void Execute()
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
        }
        private HttpStatusCode ProcessRequest(out string content)
        {
            Uri uri = _scope.GetUri(_statement.Target);

            Dictionary<string, string> headers = GetRequestHeaders();

            using (HttpRequestMessage request = new(GetHttpMethod(), uri))
            {
                ConfigureRequestHeaders(in request, in headers);

                request.Content = new StringContent(GetRequestContent(), Encoding.UTF8);

                ConfigureContentHeaders(in request, in headers);

                HttpResponseMessage response = _client.Send(request);

                using (System.IO.Stream stream = response.Content?.ReadAsStream())
                {
                    if (stream is null) { content = string.Empty; }
                    else
                    {
                        using (StreamReader reader = new(stream, Encoding.UTF8))
                        {
                            content = reader.ReadToEnd();
                        }
                    }
                }

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
            if (!_scope.TryGetValue(_target, out object value))
            {
                throw new InvalidOperationException($"Response variable {_target} is not found");
            }

            if (value is null)
            {
                value = new DataObject(2);

                if (!_scope.TrySetValue(_target, value))
                {
                    throw new InvalidOperationException($"Failed to set response variable {_target}");
                }
            }

            if (value is DataObject response)
            {
                response.SetValue("Code", ((int)code).ToString());
                response.SetValue("Value", content is null ? string.Empty : content);
            }
        }
    }
}