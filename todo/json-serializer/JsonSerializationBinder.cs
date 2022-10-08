using System;
using System.Collections.Generic;
using System.Linq;

namespace MDLP
{
    public interface ISerializationBinder
    {
        Type GetType(string typeCode);
        string GetTypeCode(Type type);
        Dictionary<string, Type> KnownTypes { get; }
    }
    public sealed class JsonSerializationBinder : ISerializationBinder
    {
        public Dictionary<string, Type> KnownTypes { get; } = new Dictionary<string, Type>();
        public JsonSerializationBinder() { }
        public Type GetType(string typeCode)
        {
            return KnownTypes.SingleOrDefault(i => i.Key == typeCode).Value;
        }
        public string GetTypeCode(Type type)
        {
            return KnownTypes.SingleOrDefault(i => i.Value == type).Key;
        }
    }
}