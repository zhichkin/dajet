using System.Collections.Concurrent;
using System.Text;

namespace DaJet.Runtime
{
    public sealed class ScriptHost
    {
        private static readonly object _cache_lock = new();
        private readonly ConcurrentDictionary<string, IProcessor> _cache = new();
        static ScriptHost() { Default = new(); }
        public static ScriptHost Default { get; }
        public bool TryRun(in string scriptPath, out string error)
        {
            error = string.Empty;

            if (_cache.TryGetValue(scriptPath, out IProcessor script))
            {
                try
                {
                    script.Process(); return true;
                }
                catch (Exception exception)
                {
                    error = ExceptionHelper.GetErrorMessage(exception); return false;
                }
            }

            try
            {
                lock (_cache_lock)
                {
                    if (_cache.TryGetValue(scriptPath, out script))
                    {
                        try
                        {
                            script.Process(); return true;
                        }
                        catch (Exception exception)
                        {
                            error = ExceptionHelper.GetErrorMessage(exception); return false;
                        }
                    }

                    string scriptCode = string.Empty;
                    string scriptFullPath = Path.Combine(AppContext.BaseDirectory, scriptPath);

                    using (StreamReader reader = new(scriptFullPath, Encoding.UTF8))
                    {
                        scriptCode = reader.ReadToEnd();
                    }

                    Dictionary<string, object> parameters = new();

                    if (!StreamFactory.TryCreateStream(in scriptCode, in parameters, out script, out error))
                    {
                        throw new Exception(error);
                    }

                    _ = _cache.TryAdd(scriptPath, script);
                }

                script.Process();
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrEmpty(error);
        }
    }
}