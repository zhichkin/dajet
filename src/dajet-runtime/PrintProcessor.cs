﻿using DaJet.Scripting.Model;

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
                    FileLogger.Default.Write(value.ToString());
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