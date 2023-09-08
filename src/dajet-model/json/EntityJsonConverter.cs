using DaJet.Data;
using DaJet.Model;
using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class EntityJsonConverter : JsonConverter<TreeNodeRecord>
    {
        private readonly IDomainModel _domain;
        public EntityJsonConverter(IDomainModel domain)
        {
            _domain = domain;
        }
        public override void Write(Utf8JsonWriter writer, TreeNodeRecord entity, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("$type", typeof(TreeNodeRecord).FullName);

            if (entity.State == PersistentState.Virtual)
            {
                writer.WriteString("State", "Virtual");
                writer.WriteString("Identity", entity.Identity.ToString().ToLowerInvariant());
                writer.WriteEndObject();

                return;
            }

            writer.WriteString("State", entity.State.ToString());
            writer.WriteString("Identity", entity.Identity.ToString().ToLowerInvariant());
            writer.WriteString("Name", entity.Name);
            writer.WriteBoolean("IsFolder", entity.IsFolder);

            if (entity.Parent is null)
            {
                writer.WriteNull("Parent");
            }
            else
            {
                writer.WritePropertyName("Parent");
                Write(writer, entity.Parent, options);
            }

            if (entity.Value is null)
            {
                writer.WriteNull("Value");
            }
            else
            {
                writer.WritePropertyName("Value");
                writer.WriteStartObject();
                writer.WriteString("$type", entity.Value.GetType().FullName);
                writer.WriteString("State", "Virtual");
                writer.WriteString("Identity", entity.Value.Identity.ToString().ToLowerInvariant());
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
        public override TreeNodeRecord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            TreeNodeRecord entity = _domain.New<TreeNodeRecord>();

            PersistentState stateValue = PersistentState.Loading;

            entity.SetLoadingState();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string name = reader.GetString();

                    if (name == "$type")
                    {
                        if (reader.Read() && reader.TokenType == JsonTokenType.String)
                        {
                            string typeName = reader.GetString();
                        }
                        else
                        {
                            throw new JsonException();
                        }
                    }
                    else if (name == "State")
                    {
                        if (reader.Read() && reader.TokenType == JsonTokenType.String)
                        {
                            string state = reader.GetString();

                            if (state == "Virtual")
                            {
                                if (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    if (reader.GetString() == "Identity")
                                    {
                                        if (reader.Read() && reader.TokenType == JsonTokenType.String)
                                        {
                                            if (Guid.TryParse(reader.GetString(), out Guid uuid))
                                            {
                                                entity.SetVirtualState(uuid);

                                                if (reader.Read() && reader.TokenType == JsonTokenType.EndObject)
                                                {
                                                    return entity;
                                                }
                                                else
                                                {
                                                    throw new JsonException();
                                                }
                                            }
                                            else
                                            {
                                                throw new JsonException();
                                            }
                                        }
                                        else
                                        {
                                            throw new JsonException();
                                        }
                                    }
                                    else
                                    {
                                        throw new JsonException();
                                    }
                                }
                                else
                                {
                                    throw new JsonException();
                                }
                            }
                            else
                            {
                                if (!Enum.TryParse(state, out stateValue))
                                {
                                    throw new JsonException();
                                }
                            }
                        }
                        else
                        {
                            throw new JsonException();
                        }
                    }
                    else if (name == "Identity")
                    {
                        if (reader.Read() && reader.TokenType == JsonTokenType.String)
                        {
                            if (Guid.TryParse(reader.GetString(), out Guid uuid))
                            {
                                typeof(EntityObject)
                                    .GetField("_identity", BindingFlags.Instance | BindingFlags.NonPublic)
                                    .SetValue(entity, uuid);
                            }
                            else
                            {
                                throw new JsonException();
                            }
                        }
                    }
                    else if (name == "Name")
                    {
                        if (reader.Read() && reader.TokenType == JsonTokenType.String)
                        {
                            entity.Name = reader.GetString();
                        }
                        else
                        {
                            throw new JsonException();
                        }
                    }
                    else if (name == "IsFolder")
                    {
                        if (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                            {
                                entity.IsFolder = reader.GetBoolean();
                            }
                            else
                            {
                                throw new JsonException();
                            }
                        }
                        else
                        {
                            throw new JsonException();
                        }
                    }
                    else if (name == "Parent")
                    {
                        if (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.Null)
                            {
                                entity.Parent = null;
                            }
                            else if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                entity.Parent = Read(ref reader, typeof(TreeNodeRecord), options);
                            }
                            else
                            {
                                throw new JsonException();
                            }
                        }
                        else
                        {
                            throw new JsonException();
                        }
                    }
                    else if (name == "Value")
                    {
                        if (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.Null)
                            {
                                entity.Value = null;
                            }
                            else if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                entity.Value = Read(ref reader, typeof(TreeNodeRecord), options);
                            }
                            else
                            {
                                throw new JsonException();
                            }
                        }
                        else
                        {
                            throw new JsonException();
                        }
                    }
                    else
                    {
                        throw new JsonException(); //TODO: skip reading property ?
                    }
                }
                else
                {
                    throw new JsonException();
                }
            }

            FieldInfo field = typeof(Persistent).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(entity, stateValue);

            entity.SetOriginalState();

            return entity;
        }
    }
}