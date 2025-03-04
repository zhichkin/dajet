﻿using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class RootProcessor : IProcessor
    {
        private readonly IProcessor _next;
        private readonly ScriptScope _scope;
        public RootProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ScriptModel)
            {
                throw new ArgumentException(nameof(ScriptModel));
            }
            
            StreamFactory.InitializeVariables(in _scope);

            _next = StreamFactory.CreateStream(in _scope);
        }
        public object ReturnValue { get; set; }
        public void LinkTo(in IProcessor next) { throw new NotImplementedException(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process()
        {
            try
            {
                _next?.Process();
            }
            catch (ReturnException _return)
            {
                ReturnValue = _return.Value; //TODO: avoid exception hack !?
            }
            finally
            {
                //TODO: root processor : dispose pipeline !?
            }
        }
    }
}