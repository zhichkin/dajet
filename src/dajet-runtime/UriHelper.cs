using System.Text;

namespace DaJet.Runtime
{
    internal static class UriHelper
    {
        internal static string GetScriptFilePath(in string fileUri)
        {
            Uri uri = new(fileUri);

            if (uri.Scheme != "file")
            {
                throw new InvalidOperationException(fileUri);
            }

            string localPath = uri.LocalPath[2..];

            string scriptPath = Path.Combine(AppContext.BaseDirectory, localPath);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                scriptPath = scriptPath.Replace('\\', '/');
            }

            return scriptPath;
        }
        internal static string GetScriptSourceCode(in string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException(scriptPath);
            }

            string script;

            using (StreamReader reader = new(scriptPath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            return script;
        }
    }
}