﻿using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class BreakException : Exception { }
    public sealed class BreakProcessor : IProcessor
    {
        private readonly ScriptScope _scope;
        private readonly BreakStatement _statement;
        public BreakProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not BreakStatement statement)
            {
                throw new ArgumentException(nameof(BreakStatement));
            }

            _statement = statement;
        }
        public void Dispose() { }
        public void Synchronize() { }
        public void LinkTo(in IProcessor next) { }
        public void Process() { throw new BreakException(); }
    }
}
