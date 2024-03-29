using DaJet.Data;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DaJet.Sqlite
{
    public sealed class NamespaceDataMapper : IDataMapper
    {
        private readonly int MY_TYPE_CODE;
        private readonly IDataSource _source;
        #region "SQL"
        private const string SELECT_BY_UUID =
            "SELECT uuid, name, parent FROM dajet_namespace WHERE uuid = @uuid;";
        private const string SELECT_BY_NAME =
            "SELECT uuid, name, parent FROM dajet_namespace WHERE name = @name;";
        private const string SELECT_BY_PARENT =
            "SELECT uuid, name, parent FROM dajet_namespace WHERE parent = @parent ORDER BY name ASC;";
        private const string INSERT_COMMAND =
            "INSERT INTO dajet_namespace (uuid, name, parent) VALUES (@uuid, @name, @parent);";
        private const string UPDATE_COMMAND =
            "UPDATE dajet_namespace SET name = @name, parent = @parent WHERE uuid = @uuid;";
        private const string DELETE_BY_UUID = "DELETE FROM dajet_namespace WHERE uuid = @uuid;";
        #endregion
        public NamespaceDataMapper(IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(NamespaceRecord));
        }
        public IEnumerable Select()
        {
            return Select(new Entity(MY_TYPE_CODE, Guid.Empty));
        }
        public IEnumerable Select(Entity parent)
        {
            List<NamespaceRecord> list = new();

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_PARENT;

                    command.Parameters.AddWithValue("parent", parent.Identity.ToString().ToLowerInvariant());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            NamespaceRecord item = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(2)))
                            };
                            item.MarkAsOriginal();

                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public EntityObject Select(string name)
        {
            NamespaceRecord record = null;

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
                            record = new NamespaceRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(2)))
                            };
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
            NamespaceRecord record = null;

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
                            record = new NamespaceRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(2)))
                            };
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
            if (entity is not NamespaceRecord record)
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
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("parent", record.Parent.Identity.ToString().ToLowerInvariant());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not NamespaceRecord record)
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
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("parent", record.Parent.Identity.ToString().ToLowerInvariant());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            throw new NotImplementedException();
        }
        public EntityObject Select(int code)
        {
            throw new NotImplementedException();
        }
    }
}