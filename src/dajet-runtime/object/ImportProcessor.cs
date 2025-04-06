using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ImportProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly ImportStatement _statement;
        public ImportProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ImportStatement statement)
            {
                throw new ArgumentException(nameof(ImportStatement));
            }

            _statement = statement;

            ImportTypeDefinitions();
        }
        public void Dispose() { _next?.Dispose(); }
        public void Process() { _next?.Process(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        private string GetScriptFilePath()
        {
            Uri uri = new(_statement.Source);

            string localPath = uri.LocalPath[2..];

            string scriptPath = Path.Combine(AppContext.BaseDirectory, localPath);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                scriptPath = scriptPath.Replace('\\', '/');
            }

            return scriptPath;
        }
        private void ImportTypeDefinitions()
        {
            string script = GetScriptFilePath();

            Dictionary<string, object> parameters = new();

            StreamManager.Execute(in script, in parameters, out _);
        }
    }
}