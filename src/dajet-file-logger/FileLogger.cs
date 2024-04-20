using System.Text;

namespace DaJet
{
    public sealed class FileLogger
    {
        private const string DEFAULT_LOG_FILE_NAME = "dajet.log";
        static FileLogger() { Default = new(); }
        public static FileLogger Default { get; }

        private uint _fileSize = 1024000; // 1 Mb
        private string _fullPath = string.Empty;
        private string _filePath = string.Empty;
        private string _catalogPath = string.Empty;
        private readonly object _syncRoot = new();
        public FileLogger()
        {
            _filePath = DEFAULT_LOG_FILE_NAME;
            _catalogPath = AppContext.BaseDirectory;
            ChangeFullPath();
        }
        public uint LogSize { get { return _fileSize; } }
        public string LogPath { get { return _fullPath; } }
        private void ChangeFullPath() { _fullPath = Path.Combine(_catalogPath, _filePath); }
        public void UseLogSize(uint bytes) { _fileSize = bytes; }
        public void UseCatalog(string catalogPath) { _catalogPath = catalogPath; ChangeFullPath(); }
        public void UseLogFile(string relativePath) { _filePath = relativePath; ChangeFullPath(); }
        public void Write(string text)
        {
            lock (_syncRoot)
            {
                try
                {
                    WriteSynchronized(text);
                }
                catch
                {
                    /* log file access error - just ignore this exception */
                }
            }
        }
        public void Write(Exception error) { Write(GetErrorMessage(error)); }
        private void WriteSynchronized(string text)
        {
            FileInfo file = new(_fullPath);

            try
            {
                if (file.Exists && file.Length > _fileSize)
                {
                    file.Delete();
                }
            }
            catch (Exception error)
            {
                text += Environment.NewLine + GetErrorMessage(error);
            }

            using (StreamWriter writer = new(_fullPath, true, Encoding.UTF8))
            {
                writer.WriteLine("[{0}] {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text);
            }
        }
        private static string GetErrorMessage(Exception error)
        {
            if (error == null)
            {
                return string.Empty;
            }

            Exception current = error;
            string message = string.Empty;

            while (current != null)
            {
                if (message != string.Empty)
                {
                    message += Environment.NewLine;
                }
                message += current.Message;

                current = current.InnerException;
            }

            return message;
        }
    }
}