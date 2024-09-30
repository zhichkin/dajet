using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class PrintProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly PrintStatement _statement;
        public PrintProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not PrintStatement statement)
            {
                throw new ArgumentException(nameof(PrintStatement));
            }
            
            _statement = statement;
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            if (StreamFactory.TryEvaluate(in _scope, _statement.Expression, out object value))
            {
                if (StreamManager.LOG_MODE == 0)
                {
                    if (value is null)
                    {
                        FileLogger.Default.Write("null");
                    }
                    else if (value is bool boolean)
                    {
                        FileLogger.Default.Write(boolean ? "true" : "false");
                    }
                    else if (value is decimal number)
                    {
                        FileLogger.Default.Write(number.ToString().Replace(',', '.'));
                    }
                    else if (value is DateTime datetime)
                    {
                        FileLogger.Default.Write(datetime.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else if (value is byte[] binary)
                    {
                        string hex = "0x";

                        if (binary.Length == 0)
                        {
                            FileLogger.Default.Write(hex);
                        }
                        else
                        {
                            FileLogger.Default.Write(hex + DbUtilities.ByteArrayToString(binary));
                        }
                    }
                    else
                    {
                        FileLogger.Default.Write(value.ToString());
                    }
                }
                else
                {
                    Console.WriteLine(value.ToString());
                }
            }

            _next?.Process();
        }
    }
}