using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Stream
{
    public sealed class ExecuteProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _stream;
        private readonly StreamScope _scope;
        private readonly ExecuteStatement _statement;
        public ExecuteProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ExecuteStatement statement)
            {
                throw new ArgumentException(nameof(ExecuteStatement));
            }

            _statement = statement;

            _stream = PrepareScript();
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            try
            {
                _stream?.Process();
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(error);
            }

            _next?.Process();
        }
        private IProcessor PrepareScript()
        {
            Uri uri = _scope.GetUri(_statement.Uri);

            string file = string.Concat(".", uri.LocalPath.AsSpan(1));

            file = file.Replace('\\', '/'); // Linux compliant path

            if (!File.Exists(file))
            {
                FileLogger.Default.Write($"File not found {file}"); return null;
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

            StreamScope child = _scope.Create(in model);

            foreach (var variable in child.Variables)
            {
                if (_scope.TryGetValue(variable.Key, out _))
                {
                    child.Variables.Remove(variable.Key); //NOTE: use parent scope's variables
                }
            }

            StreamFactory.InitializeVariables(in child); //NOTE: DECLARE @variable = SELECT ...

            return StreamFactory.CreateStream(in child);
        }
    }
}