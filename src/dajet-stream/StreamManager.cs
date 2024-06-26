﻿using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Diagnostics;
using System.Text;

namespace DaJet.Stream
{
    public static class StreamManager
    {
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

            Stopwatch watch = new();

            watch.Start();

            if (TryCreateStream(in script, out IProcessor stream, out string error))
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
                error = ExceptionHelper.GetErrorMessageAndStackTrace(exception);
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
    }
}