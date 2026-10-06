using System.Text;

namespace DaJet
{
    /// <summary>
    /// Уровень записи журнала DaJet. Выводится вторым полем формата:
    /// [время] [УРОВЕНЬ] [имя скрипта] текст.
    /// </summary>
    public enum LogLevel
    {
        INFO = 0,
        WARNING = 1,
        ERROR = 2
    }

    /// <summary>
    /// Назначение журнала DaJet: файл (по умолчанию) или стандартный поток вывода.
    /// </summary>
    public enum LogTarget
    {
        File = 0,
        Console = 1
    }

    public sealed class FileLogger
    {
        private const string DEFAULT_LOG_FILE_NAME = "dajet.log";
        static FileLogger() { Default = new(); }
        public static FileLogger Default { get; }

        //NOTE: имя обрабатываемого скрипта для текущего логического потока выполнения.
        //NOTE: устанавливается StreamManager при запуске потока и позволяет писать
        //NOTE: имя скрипта в каждой записи журнала без правки обработчиков и адаптеров.
        private static readonly AsyncLocal<string> _source = new();
        public static void UseSource(string source) { _source.Value = source; }
        public static string Source { get { return _source.Value ?? string.Empty; } }

        private uint _fileSize = 1024000; // 1 Mb
        private string _fullPath = string.Empty;
        private string _filePath = string.Empty;
        private string _catalogPath = string.Empty;
        private LogTarget _target = LogTarget.File;
        private readonly object _syncRoot = new();
        public FileLogger()
        {
            _filePath = DEFAULT_LOG_FILE_NAME;
            _catalogPath = AppContext.BaseDirectory;
            ChangeFullPath();
        }
        public uint LogSize { get { return _fileSize; } }
        public string LogPath { get { return _fullPath; } }
        public LogTarget Target { get { return _target; } }
        private void ChangeFullPath() { _fullPath = Path.Combine(_catalogPath, _filePath); }
        public void UseLogSize(uint bytes) { _fileSize = bytes; }
        public void UseCatalog(string catalogPath) { _catalogPath = catalogPath; ChangeFullPath(); }
        public void UseLogFile(string relativePath) { _filePath = relativePath; ChangeFullPath(); }
        public void UseLogTarget(LogTarget target) { _target = target; }
        public void Write(string text)
        {
            WriteRecord(GetMarkerLevel(text), Source, text);
        }
        public void Write(string text, LogLevel level)
        {
            WriteRecord(level, Source, text);
        }
        public void Write(LogLevel level, string text)
        {
            WriteRecord(level, Source, text);
        }
        public void Write(Exception error)
        {
            WriteRecord(LogLevel.ERROR, Source, GetErrorMessage(error));
        }
        private void WriteRecord(LogLevel level, string source, string text)
        {
            lock (_syncRoot)
            {
                try
                {
                    WriteSynchronized(FormatRecord(level, source, text));
                }
                catch
                {
                    /* log file access error - just ignore this exception */
                }
            }
        }
        private static string FormatRecord(LogLevel level, string source, string text)
        {
            string levelText;

            switch (level)
            {
                case LogLevel.ERROR: levelText = "ERROR"; break;
                case LogLevel.WARNING: levelText = "WARNING"; break;
                default: levelText = "INFO"; break;
            }

            if (string.IsNullOrEmpty(source)) { source = "-"; }

            //NOTE: одна запись журнала - одна строка: переводы строк внутри текста экранируются
            string message = (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ');

            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{levelText}] [{source}] {message}";
        }
        private static LogLevel GetMarkerLevel(string text)
        {
            if (string.IsNullOrEmpty(text)) { return LogLevel.INFO; }

            //NOTE: обратная совместимость: старые вызовы Write(string) уже содержат
            //NOTE: маркер уровня в тексте - используем его, чтобы не править все вызовы.
            if (text.StartsWith("[ERROR]")
             || text.StartsWith("[FATAL ERROR]")
             || text.StartsWith("[DISPOSE ERROR]")
             || text.StartsWith("[STREAM][FAULTED]"))
            {
                return LogLevel.ERROR;
            }

            if (text.StartsWith("[WARNING]")
             || text.StartsWith("[UNSUPPORTED]")
             || text.StartsWith("[CONFIG] NOT FOUND"))
            {
                return LogLevel.WARNING;
            }

            return LogLevel.INFO;
        }
        private void WriteSynchronized(string record)
        {
            if (_target == LogTarget.Console)
            {
                Console.Out.WriteLine(record);
                return;
            }

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
                record += Environment.NewLine + GetErrorMessage(error);
            }

            using (StreamWriter writer = new(_fullPath, true, Encoding.UTF8))
            {
                writer.WriteLine(record);
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
