using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace DaJet.Metadata
{
    internal static class FileLogger
    {
        private const string LOG_FILE_NAME = "dajet-metadata.log";
        private static string _filePath;
        private static object _syncLog = new object();
        private static string GetFilePath()
        {
            if (_filePath != null)
            {
                return _filePath;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            string catalogPath = Path.GetDirectoryName(assembly.Location);
            _filePath = Path.Combine(catalogPath, LOG_FILE_NAME);

            return _filePath;
        }
        internal static int LogSize { get; set; } = 1024000;
        internal static void UseCatalog(string catalogPath)
        {
            _filePath = Path.Combine(catalogPath, LOG_FILE_NAME);
        }
        internal static void Log(string token, string text)
        {
            Log(string.Format("[{0}] {1}", token, text));
        }
        internal static void Log(string text)
        {
            lock (_syncLog)
            {
                try
                {
                    LogSyncronized(text);
                }
                catch { /* log file access error - just ignore this exception */ }
            }
        }
        private static void LogSyncronized(string text)
        {
            string filePath = GetFilePath();
            FileInfo file = new FileInfo(filePath);

            try
            {
                if (file.Exists && file.Length > LogSize)
                {
                    file.Delete();
                }
            }
            catch (Exception ex)
            {
                text += Environment.NewLine + GetErrorText(ex);
            }

            using (StreamWriter writer = new StreamWriter(GetFilePath(), true, Encoding.UTF8))
            {
                writer.WriteLine("[{0}] {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text);
            }
        }
        private static string GetErrorText(Exception ex)
        {
            string errorText = string.Empty;

            Exception error = ex;
            while (error != null)
            {
                errorText += (errorText == string.Empty) ? error.Message : Environment.NewLine + error.Message;
                error = error.InnerException;
            }

            return errorText;
        }
    }
}