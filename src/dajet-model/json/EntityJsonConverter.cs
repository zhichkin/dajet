using DaJet.Data;
using DaJet.Model;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class EntityJsonConverter : JsonConverter<EntityObject>
    {
        private readonly IDomainModel _domain;
        public EntityJsonConverter(IDomainModel domain)
        {
            _domain = domain;
        }
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsSubclassOf(typeof(EntityObject));
        }
        public override void Write(Utf8JsonWriter writer, EntityObject entity, JsonSerializerOptions options)
        {
            Type type = _domain.GetEntityType(entity.TypeCode);

            if (type is null)
            {
                JsonSerializer.Serialize(writer, entity);
            }
            else
            {
                JsonSerializer.Serialize(writer, entity, type);
            }
        }
        public override EntityObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, typeToConvert) as EntityObject;
        }
    }
}