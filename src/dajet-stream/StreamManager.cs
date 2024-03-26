using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Diagnostics;
using System.Text;

namespace DaJet.Stream
{
    public static class StreamManager
    {
        private static readonly Dictionary<string, IProcessor> _streams = new();
        public static void Activate(in string path)
        {
            if (Directory.Exists(path))
            {
                ActivateStreams(in path);
            }
            else
            {
                FileLogger.Default.Write($"[ERROR] {path}");
            }
        }
        private static void ActivateStreams(in string path)
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.sql"))
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

            if (TryCreateStream(in script, out IProcessor stream, out string error))
            {
                _ = Task.Factory.StartNew(stream.Process, TaskCreationOptions.LongRunning);

                _ = _streams.TryAdd(file, stream);

                FileLogger.Default.Write($"[STREAM] {file}");
            }
            else
            {
                FileLogger.Default.Write($"[ERROR] {file}");
                FileLogger.Default.Write(error);
            }
        }
        public static bool TryCreateStream(in string script, out IProcessor stream, out string error)
        {
            error = null;
            stream = null;

            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out error))
                {
                    return false;
                }
            }

            try
            {
                stream = StreamFactory.Create(in model);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return stream is not null;
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
                    FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
                }
            }

            _streams.Clear();
        }

        private static void StreamFile(string filePath)
        {
            FileInfo file = new(filePath);

            Console.WriteLine($"Execute script from file: {file.FullName}");

            string script;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            StreamManager.Process(in script);
        }
        public static void Process(in string script)
        {
            Stopwatch watch = new();

            watch.Start();

            IProcessor stream = CreateStream(in script);

            watch.Stop();

            long elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline assembled in {elapsed} ms");

            watch.Restart();

            stream.Process();

            watch.Stop();

            elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline executed in {elapsed} ms");
        }
        private static IProcessor CreateStream(in string script)
        {
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out string error))
                {
                    Console.WriteLine(error);
                }
            }

            return StreamFactory.Create(in model);
        }
    }
}