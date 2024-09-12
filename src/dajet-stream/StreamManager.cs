using System.Diagnostics;
using System.Text;

namespace DaJet.Stream
{
    public static class StreamManager
    {
        private const string DAJET_SCRIPT_FILE_EXTENSION = "*.djs";

        public static int LOG_MODE = 0; // 0 - file (default), 1 - console
        public static void LogToFile() { LOG_MODE = 0; }
        public static void LogToConsole() { LOG_MODE = 1; }

        private static readonly Dictionary<string, IProcessor> _streams = new();
        public static void Serve(in string path)
        {
            if (Directory.Exists(path))
            {
                ActivateStreams(in path);

                DisposeStreams();
            }
            else
            {
                FileLogger.Default.Write($"[ERROR] {path}");
            }
        }
        private static void ActivateStreams(in string path)
        {
            foreach (string file in Directory.EnumerateFiles(path, DAJET_SCRIPT_FILE_EXTENSION))
            {
                ActivateStream(in file);
            }

            foreach (string catalog in Directory.EnumerateDirectories(path))
            {
                ActivateStreams(in catalog);
            }
        }
        private static void ActivateStream(in string file)
        {
            if (_streams.ContainsKey(file)) { return; }

            if (!File.Exists(file)) { return; }

            string script;

            using (StreamReader reader = new(file, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            Stopwatch watch = new();

            watch.Start();

            if (StreamFactory.TryCreateStream(in script, out IProcessor stream, out string error))
            {
                _ = Task.Factory.StartNew(stream.Process, TaskCreationOptions.LongRunning);

                _ = _streams.TryAdd(file, stream);

                watch.Stop();

                FileLogger.Default.Write($"[STREAM][Assembled in {watch.ElapsedMilliseconds} ms] {file}");
            }
            else
            {
                FileLogger.Default.Write($"[ERROR] {file}");
                FileLogger.Default.Write(error);
            }
        }
        public static void Dispose()
        {
            foreach (var stream in _streams)
            {
                try
                {
                    stream.Value.Dispose();

                    FileLogger.Default.Write($"[DISPOSED] {stream.Key}");
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(error);
                }
            }

            _streams.Clear();
        }
        private static void DisposeStreams()
        {
            List<string> keys = new();

            foreach (var stream in _streams)
            {
                if (!File.Exists(stream.Key))
                {
                    keys.Add(stream.Key);
                }
            }

            foreach (string file in keys)
            {
                if (_streams.Remove(file, out IProcessor stream) && stream is not null)
                {
                    try
                    {
                        stream.Dispose();

                        FileLogger.Default.Write($"[DISPOSED] {file}");
                    }
                    catch (Exception error)
                    {
                        FileLogger.Default.Write($"[DISPOSE ERROR] {file}");
                        FileLogger.Default.Write(error);
                    }
                }
            }
        }

        public static void Execute(in string script)
        {
            if (!StreamFactory.TryCreateStream(in script, out IProcessor stream, out string error))
            {
                throw new Exception(error);
            }

            stream.Process();
        }
        public static bool TryExecute(in string filePath, out string error)
        {
            error = null;
            string script;

            try
            {
                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
                }

                Execute(in script);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);

                if (LOG_MODE == 0) { FileLogger.Default.Write(error); }
            }

            return string.IsNullOrEmpty(error);
        }
        public static bool TryExecute(in string filePath, out object result, out string error)
        {
            error = null;
            result = null;
            
            try
            {
                string script;

                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
                }

                if (!StreamFactory.TryCreateStream(in script, out IProcessor stream, out error))
                {
                    return false;
                }

                stream.Process();

                if (stream is RootProcessor root)
                {
                    result = root.GetReturnValue();
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);

                if (LOG_MODE == 0) { FileLogger.Default.Write(error); }
            }

            return string.IsNullOrEmpty(error);
        }
    }
}