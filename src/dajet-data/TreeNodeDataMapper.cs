using DaJet.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Data
{
    public sealed class TreeNodeDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "maintree (uuid TEXT NOT NULL, parent TEXT NOT NULL, name TEXT NOT NULL, is_folder INTEGER NOT NULL, " +
            "entity_type INTEGER NOT NULL, entity_uuid TEXT NOT NULL, PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE uuid = @uuid;";
        private const string SELECT_BY_PARENT =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE parent = @value;";
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
        public void Insert(TreeNodeRecord entity)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_COMMAND;

                    command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", entity.Parent.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", entity.Name);
                    command.Parameters.AddWithValue("is_folder", entity.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("entity_type", entity.Value.TypeCode);
                    command.Parameters.AddWithValue("entity_uuid", entity.Value.Identity.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(TreeNodeRecord entity)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_COMMAND;

                    command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", entity.Parent.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", entity.Name);
                    command.Parameters.AddWithValue("is_folder", entity.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("entity_type", entity.Value.TypeCode);
                    command.Parameters.AddWithValue("entity_uuid", entity.Value.Identity.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            DeleteRecursively(entity);
        }
        public void DeleteTreeNode(Entity entity)
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
        public void DeleteRecursively(Entity entity)
        {
            List<TreeNodeRecord> children = Select("parent", entity);

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
        public List<TreeNodeRecord> Select()
        {
            return Select("parent", Entity.Undefined);
        }
        public TreeNodeRecord Select(Entity entity)
        {
            TreeNodeRecord record = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_UUID;

                    command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());

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
        public List<TreeNodeRecord> Select(string propertyName, Entity value)
        {
            List<TreeNodeRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_PARENT;

                    command.Parameters.AddWithValue("value", value.Identity.ToString().ToLower());

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
    }
}