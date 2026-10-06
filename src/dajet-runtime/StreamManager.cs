using System.Diagnostics;
using System.Text;

namespace DaJet.Runtime
{
    public static class StreamManager
    {
        private const string DAJET_SCRIPT_FILE_EXTENSION = "*.djs";
        private static readonly object _syncRoot = new();
        private static readonly Dictionary<string, IProcessor> _streams = new();

        //NOTE: учёт отказов потоков для нарастающей паузы между пересборками (см. _fault_delays)
        private static readonly Dictionary<string, FaultInfo> _faults = new();
        private static readonly int[] _fault_delays = { 0, 10, 30, 60, 120, 300, 600 }; // seconds
        private sealed class FaultInfo
        {
            public int Count { get; set; }
            public DateTime NextAttemptUtc { get; set; }
        }

        public static int LOG_MODE = 0; // 0 - file (default), 1 - console
        public static int StopTimeoutSeconds { get; set; } = 30; // wait for a stream to stop
        public static void LogToFile()
        {
            LOG_MODE = 0;
            FileLogger.Default.UseLogTarget(LogTarget.File);
        }
        public static void LogToConsole()
        {
            LOG_MODE = 1;
            FileLogger.Default.UseLogTarget(LogTarget.Console);
        }
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
            string path = file;

            FileLogger.UseSource(file);

            lock (_syncRoot)
            {
                if (_streams.ContainsKey(file)) { return; }

                if (_faults.TryGetValue(file, out FaultInfo fault) && fault.NextAttemptUtc > DateTime.UtcNow)
                {
                    return; //NOTE: нарастающая пауза перед повторной сборкой упавшего потока
                }
            }

            if (!File.Exists(file)) { return; }

            string script;

            using (StreamReader reader = new(file, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            Stopwatch watch = new();

            watch.Start();

            Dictionary<string, object> parameters = new();

            if (StreamFactory.TryCreateStream(in script, in parameters, out IProcessor stream, out string error))
            {
                lock (_syncRoot)
                {
                    _streams[file] = stream;
                    _faults.Remove(file); // success - reset the fault counter
                }

                _ = Task.Factory.StartNew(() => ProcessStream(in path, in stream), TaskCreationOptions.LongRunning);

                watch.Stop();

                FileLogger.Default.Write($"[STREAM][Assembled in {watch.ElapsedMilliseconds} ms] {file}");
            }
            else
            {
                FileLogger.Default.Write($"[ERROR] {file}");
                FileLogger.Default.Write(error);
            }
        }
        private static void ProcessStream(in string file, in IProcessor stream)
        {
            FileLogger.UseSource(file);

            try
            {
                stream.Process();

                //NOTE: нормально завершившийся поток (например, DDL без CONSUME) остаётся
                //NOTE: в реестре, иначе он выполнялся бы заново на каждом Refresh.
            }
            catch (Exception error)
            {
                //NOTE: упавший поток убираем из реестра, чтобы следующий Refresh собрал его заново
                lock (_syncRoot)
                {
                    _streams.Remove(file);

                    if (!_faults.TryGetValue(file, out FaultInfo fault))
                    {
                        fault = new FaultInfo();
                        _faults[file] = fault;
                    }

                    fault.Count++;

                    int index = fault.Count < _fault_delays.Length ? fault.Count : _fault_delays.Length - 1;

                    fault.NextAttemptUtc = DateTime.UtcNow.AddSeconds(_fault_delays[index]);
                }

                FileLogger.Default.Write(LogLevel.ERROR,
                    $"[STREAM][FAULTED] {file}: {ExceptionHelper.GetErrorMessage(error)}");
            }
        }
        public static void Dispose()
        {
            KeyValuePair<string, IProcessor>[] streams;

            lock (_syncRoot)
            {
                streams = new KeyValuePair<string, IProcessor>[_streams.Count];

                int index = 0;

                foreach (var stream in _streams) { streams[index++] = stream; }

                _streams.Clear();
                _faults.Clear();
            }

            //NOTE: гасим потоки параллельно и ждём завершения всех (см. ConsumeProcessor.Dispose)
            _ = Parallel.ForEach(streams, stream =>
            {
                FileLogger.UseSource(stream.Key);

                try
                {
                    stream.Value.Dispose();

                    FileLogger.Default.Write($"[DISPOSED] {stream.Key}");
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(error);
                }
            });
        }
        private static void DisposeStreams()
        {
            List<string> keys = new();

            lock (_syncRoot)
            {
                foreach (var stream in _streams)
                {
                    if (!File.Exists(stream.Key))
                    {
                        keys.Add(stream.Key);
                    }
                }
            }

            foreach (string file in keys)
            {
                IProcessor stream = null;

                lock (_syncRoot)
                {
                    if (_streams.Remove(file, out IProcessor value)) { stream = value; }
                }

                if (stream is not null)
                {
                    FileLogger.UseSource(file);

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

        public static void Execute(in string filePath, in Dictionary<string, object> parameters, out object result)
        {
            result = null;

            FileLogger.UseSource(filePath);

            string script;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            if (!StreamFactory.TryCreateStream(in script, in parameters, out IProcessor stream, out string error))
            {
                throw new Exception(error);
            }

            stream.Process();

            if (stream is RootProcessor root)
            {
                result = root.ReturnValue;
            }
        }
        public static bool TryExecute(in string filePath, in Dictionary<string, object> parameters, out object result, out string error)
        {
            error = null;
            result = null;
            
            try
            {
                Execute(in filePath, in parameters, out result);
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
