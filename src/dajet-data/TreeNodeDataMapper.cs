using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class TreeNodeDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "maintree (uuid TEXT NOT NULL, parent TEXT NOT NULL, name TEXT NOT NULL, is_folder INTEGER NOT NULL, " +
            "entity_type INTEGER NOT NULL, entity_uuid TEXT NOT NULL, PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE uuid = @uuid;";
        private const string SELECT_BY_PARENT =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE parent = @parent;";
        private const string SELECT_BY_NAME =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE parent = @parent AND name = @name LIMIT 1;";
        private const string INSERT_COMMAND =
            "INSERT INTO maintree (uuid, parent, name, is_folder, entity_type, entity_uuid) " +
            "VALUES (@uuid, @parent, @name, @is_folder, @entity_type, @entity_uuid);";
        private const string UPDATE_COMMAND =
            "UPDATE maintree SET " +
            "parent = @parent, name = @name, is_folder = @is_folder, " +
            "entity_type = @entity_type, entity_uuid = @entity_uuid " +
            "WHERE uuid = @uuid;";
        private const string DELETE_COMMAND = "DELETE FROM maintree WHERE uuid = @uuid;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly string _connectionString;
        private readonly IDomainModel _domain;
        public TreeNodeDataMapper(IDomainModel domain, string connectionString)
        {
            _connectionString = connectionString;

            ConfigureDatabase();

            _domain = domain;

            MY_TYPE_CODE = _domain.GetTypeCode(typeof(TreeNodeRecord));
        }
        private void ConfigureDatabase()
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = CREATE_TABLE_COMMAND;

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        public void Insert(EntityObject entity)
        {
            if (entity is not TreeNodeRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", record.Parent.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("is_folder", record.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("entity_type", record.Value.TypeCode);
                    command.Parameters.AddWithValue("entity_uuid", record.Value.Identity.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not TreeNodeRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", record.Parent.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("is_folder", record.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("entity_type", record.Value.TypeCode);
                    command.Parameters.AddWithValue("entity_uuid", record.Value.Identity.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            DeleteRecursively(entity);
        }
        private void DeleteTreeNode(Entity entity)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_COMMAND;

                    command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        private void DeleteRecursively(Entity entity)
        {
            List<TreeNodeRecord> children = Select(entity) as List<TreeNodeRecord>;

            foreach (TreeNodeRecord child in children)
            {
                if (child.IsFolder)
                {
                    DeleteRecursively(child.GetEntity());
                }
                else
                {
                    DeleteTreeNode(child.GetEntity());
                }
            }

            DeleteTreeNode(entity);
        }
        public IEnumerable Select()
        {
            return Select(Entity.Undefined);
        }
        public EntityObject Select(Guid identity)
        {
            TreeNodeRecord record = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_UUID;

                    command.Parameters.AddWithValue("uuid", identity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new TreeNodeRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(1))),
                                Name = reader.GetString(2),
                                IsFolder = (reader.GetInt64(3) == 1L)
                            };

                            int code = (int)reader.GetInt64(4);
                            Guid uuid = new(reader.GetString(5));
                            record.Value = new Entity(code, uuid);

                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public IEnumerable Select(Entity owner)
        {
            List<TreeNodeRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_PARENT;

                    command.Parameters.AddWithValue("parent", owner.Identity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TreeNodeRecord record = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(1))),
                                Name = reader.GetString(2),
                                IsFolder = (reader.GetInt64(3) == 1L)
                            };

                            int code = (int)reader.GetInt64(4);
                            Guid uuid = new(reader.GetString(5));
                            record.Value = new Entity(code, uuid);

                            record.MarkAsOriginal();

                            list.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public EntityObject Select(string name)
        {
            string[] segments = name.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            Entity parent = Entity.Undefined;
            TreeNodeRecord current = null;

            foreach (string segment in segments)
            {
                current = Select(parent, segment);

                if (current is null)
                {
                    return null; // not found
                }

                parent = current.GetEntity();
            }

            return current;
        }
        private TreeNodeRecord Select(Entity parent, string name)
        {
            TreeNodeRecord record = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_NAME;

                    command.Parameters.AddWithValue("name", name);
                    command.Parameters.AddWithValue("parent", parent.Identity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new TreeNodeRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(1))),
                                Name = reader.GetString(2),
                                IsFolder = (reader.GetInt64(3) == 1L)
                            };

                            int code = (int)reader.GetInt64(4);
                            Guid uuid = new(reader.GetString(5));
                            record.Value = new Entity(code, uuid);

                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
    }
}