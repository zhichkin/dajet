﻿namespace DaJet.Flow
{
    public abstract class TransformerBlock<TInput, TOutput> : Configurable, IInputBlock<TInput>, IOutputBlock<TOutput>
    {
        private IInputBlock<TOutput> _next;
        public void LinkTo(in IInputBlock<TOutput> next) { _next = next; }
        public void Process(in TInput input)
        {
            _Transform(in input, out TOutput output);
            _next?.Process(in output);
        }
        protected abstract void _Transform(in TInput input, out TOutput output);
        public void Synchronize()
        {
            _next?.Synchronize();
            _Synchronize();
        }
        protected virtual void _Synchronize()
        {
            // do nothing by default
        }
    }
}