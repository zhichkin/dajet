using DaJet.Data;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DaJet.Sqlite
{
    public sealed class EntityDataMapper : IDataMapper
    {
        private readonly int MY_TYPE_CODE;
        private readonly IDataSource _source;
        #region "SQL"
        private const string SELECT_BY_UUID =
            "SELECT uuid, type, code, name, table_name, parent_type, parent_uuid FROM dajet_entity WHERE uuid = @uuid;";
        private const string SELECT_BY_CODE =
            "SELECT uuid, type, code, name, table_name, parent_type, parent_uuid FROM dajet_entity WHERE code = @code;";
        private const string SELECT_BY_NAME =
            "SELECT uuid, type, code, name, table_name, parent_type, parent_uuid FROM dajet_entity WHERE name = @name;";
        private const string SELECT_BY_PARENT =
            "SELECT uuid, type, code, name, table_name, parent_type, parent_uuid FROM dajet_entity " +
            "WHERE parent_type = @parent_type AND parent_uuid = @parent_uuid ORDER BY name ASC;";
        private const string INSERT_COMMAND =
            "INSERT INTO dajet_entity (uuid, type, code, name, table_name, parent_type, parent_uuid) " +
            "VALUES (@uuid, @type, @code, @name, @table_name, @parent_type, @parent_uuid);";
        private const string UPDATE_COMMAND =
            "UPDATE dajet_entity SET type = @type, code = @code, name = @name, table_name = @table_name, " +
            "parent_type = @parent_type, parent_uuid = @parent_uuid WHERE uuid = @uuid;";
        private const string DELETE_COMMAND =
            "DELETE FROM dajet_entity WHERE uuid = @uuid;";
        #endregion
        public EntityDataMapper(IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(EntityRecord));
        }
        public IEnumerable Select() { throw new NotImplementedException(); }
        public IEnumerable Select(Entity parent)
        {
            List<EntityRecord> list = new();

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_PARENT;

                    command.Parameters.AddWithValue("parent_type", parent.TypeCode);
                    command.Parameters.AddWithValue("parent_uuid", parent.Identity.ToString().ToLowerInvariant());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            EntityRecord item = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Type = (int)reader.GetInt64(1),
                                Code = (int)reader.GetInt64(2),
                                Name = reader.GetString(3),
                                Table = reader.GetString(4)
                            };

                            int code = (int)reader.GetInt64(5);
                            Guid uuid = new(reader.GetString(6));
                            item.Parent = new Entity(code, uuid);

                            item.MarkAsOriginal();

                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public EntityObject Select(int code)
        {
            EntityRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_CODE;

                    command.Parameters.AddWithValue("code", code);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new EntityRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Type = (int)reader.GetInt64(1),
                                Code = (int)reader.GetInt64(2),
                                Name = reader.GetString(3),
                                Table = reader.GetString(4),
                                Parent = new Entity((int)reader.GetInt64(5), new Guid(reader.GetString(6)))
                            };

                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public EntityObject Select(string name)
        {
            EntityRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_NAME;

                    command.Parameters.AddWithValue("name", name);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new EntityRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Type = (int)reader.GetInt64(1),
                                Code = (int)reader.GetInt64(2),
                                Name = reader.GetString(3),
                                Table = reader.GetString(4)
                            };

                            int code = (int)reader.GetInt64(5);
                            Guid uuid = new(reader.GetString(6));
                            record.Parent = new Entity(code, uuid);

                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public EntityObject Select(Guid idenity)
        {
            EntityRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_UUID;

                    command.Parameters.AddWithValue("uuid", idenity.ToString().ToLowerInvariant());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new EntityRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Type = (int)reader.GetInt64(1),
                                Code = (int)reader.GetInt64(2),
                                Name = reader.GetString(3),
                                Table = reader.GetString(4)
                            };

                            int code = (int)reader.GetInt64(5);
                            Guid uuid = new(reader.GetString(6));
                            record.Parent = new Entity(code, uuid);

                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public void Insert(EntityObject entity)
        {
            if (entity is not EntityRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("type", record.Type);
                    command.Parameters.AddWithValue("code", record.Code);
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("table_name", record.Table);
                    command.Parameters.AddWithValue("parent_type", record.Parent.TypeCode);
                    command.Parameters.AddWithValue("parent_uuid", record.Parent.Identity.ToString().ToLowerInvariant());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not EntityRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("type", record.Type);
                    command.Parameters.AddWithValue("code", record.Code);
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("table_name", record.Table);
                    command.Parameters.AddWithValue("parent_type", record.Parent.TypeCode);
                    command.Parameters.AddWithValue("parent_uuid", record.Parent.Identity.ToString().ToLowerInvariant());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_COMMAND;

                    command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLowerInvariant());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
    }
}