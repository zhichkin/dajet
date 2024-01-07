using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/preserve-references?pivots=dotnet-7-0
    public class JsonReferenceResolver : ReferenceResolver
    {
        private readonly IDictionary<Guid, object> _map_id_to_object = new Dictionary<Guid, object>();
        private readonly IDictionary<object, Guid> _map_object_to_id = new Dictionary<object, Guid>();

        public override void AddReference(string reference, object value)
        {
            Guid id = new(reference);
            _map_id_to_object[id] = value;
        }
        public override string GetReference(object value, out bool alreadyExists)
        {
            if (alreadyExists = _map_object_to_id.TryGetValue(value, out Guid id))
            {
                return id.ToString().ToLowerInvariant();
            }

            id = Guid.NewGuid();
            _map_object_to_id.Add(value, id);

            return id.ToString().ToLowerInvariant();
        }
        public override object ResolveReference(string reference)
        {
            Guid id = new(reference);

            if (_map_id_to_object.TryGetValue(id, out object value))
            {
                return value;
            }

            return null;
        }
    }
}