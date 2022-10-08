using System;

namespace MDLP
{
    public sealed class OptionalAttribute : Attribute { }
    public interface IOptional
    {
        object Value { get; set; }
        bool HasValue { get; set; }
    }
    public sealed class Optional<T> : IOptional
    {
        private T _value = default;
        private bool _hasValue = false;
        public Optional() { }
        public Optional(T value) { Value = value; }
        object IOptional.Value { get { return _value; } set { Value = (T)value; } }
        public T Value { get { return _value; } set { _value = value; HasValue = true; } }
        public bool HasValue
        {
            get { return _hasValue; }
            set
            {
                _hasValue = value;
                if (!_hasValue) { _value = default; }
            }
        }
    }
}