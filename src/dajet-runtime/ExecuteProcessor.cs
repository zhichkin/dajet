using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Runtime
{
    public sealed class ExecuteProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _script;
        private ScriptScope _scope;
        private readonly ScriptScope _parent; // caller
        private readonly ExecuteStatement _statement;
        public ExecuteProcessor(in ScriptScope scope)
        {
            _parent = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_parent.Owner is not ExecuteStatement statement)
            {
                throw new ArgumentException(nameof(ExecuteStatement));
            }

            _statement = statement;
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            try
            {
                _script ??= PrepareScript(); //THINK: avoid recursive script invocation !!!

                ConfigureInputParameters(); // copy input variable values from caller's scope

                _script?.Process();
            }
            catch (ReturnException)
            {
                object value = _scope.GetReturnValue();

                if (_statement.Return is null)
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Return variable is not defined");
                }

                if (!_parent.TrySetValue(_statement.Return.Identifier, value)) // set return value to the caller's scope variable
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error returning into {_statement.Return.Identifier}");
                }

                ConfigureReturnObjectSchema();

                _scope.SetReturnValue(null);
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(error); throw;
            }

            _next?.Process();
        }
        private IProcessor PrepareScript()
        {
            Uri uri = _parent.GetUri(_statement.Uri);

            string file = Path.Combine(AppContext.BaseDirectory, uri.LocalPath[2..]);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                file = file.Replace('\\', '/'); // Linux compliant path
            }

            if (!File.Exists(file))
            {
                throw new InvalidOperationException($"File not found {file}");
            }

            string script;

            using (StreamReader reader = new(file, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            if (!new ScriptParser().TryParse(in script, out ScriptModel model, out string error))
            {
                FileLogger.Default.Write(error); return null;
            }

            _scope = _parent.Create(in model);
            StreamFactory.InitializeVariables(in _scope);
            return StreamFactory.CreateStream(in _scope);
        }
        private void ConfigureInputParameters()
        {
            if (_statement.Parameters is null) { return; }

            foreach (ColumnExpression parameter in _statement.Parameters)
            {
                string identifier = "@" + parameter.Alias;

                if (!_scope.Variables.ContainsKey(identifier))
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Parameter {identifier} is not found");
                }

                if (!StreamFactory.TryEvaluate(in _parent, parameter.Expression, out object value))
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error reading parameter {identifier}");
                }

                if (!_scope.TrySetValue(in identifier, in value))
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error setting parameter {identifier}");
                }
            }
        }
        private void ConfigureReturnObjectSchema()
        {
            if (_statement.Return is null) { return; }

            string identifier = _statement.Return.Identifier;

            if (!_parent.TryGetDeclaration(in identifier, out _, out DeclareStatement target))
            {
                throw new InvalidOperationException($"Declaration of {identifier} is not found");
            }

            if (_scope.TryGetDeclaration(in identifier, out _, out DeclareStatement source))
            {
                //TODO: get return value schema from return statement
                target.Type.Binding = source.Type.Binding;
            }
        }
    }
}