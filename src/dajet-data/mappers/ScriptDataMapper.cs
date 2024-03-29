using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class ScriptDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_SCRIPT = "CREATE TABLE IF NOT EXISTS " +
            "scripts (uuid TEXT NOT NULL, owner TEXT NOT NULL, parent TEXT NOT NULL, " +
            "is_folder INTEGER NOT NULL, name TEXT NOT NULL, script TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string INSERT_COMMAND = "INSERT INTO scripts (uuid, owner, parent, is_folder, name, script) VALUES (@uuid, @owner, @parent, @is_folder, @name, @script);";
        private const string UPDATE_COMMAND = "UPDATE scripts SET owner = @owner, parent = @parent, is_folder = @is_folder, name = @name, script = @script WHERE uuid = @uuid;";
        private const string DELETE_COMMAND = "DELETE FROM scripts WHERE uuid = @uuid;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE uuid = @uuid;";
        private const string SELECT_BY_OWNER =
            "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE owner = @owner " +
            "AND parent = '00000000-0000-0000-0000-000000000000' ORDER BY is_folder ASC, name ASC;";
        private const string SELECT_BY_OWNER_AND_NAME =
            "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE owner = @owner " +
            "AND parent = '00000000-0000-0000-0000-000000000000' AND name = @name LIMIT 1;";
        private const string SELECT_BY_PARENT =
            "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE parent = @parent ORDER BY is_folder ASC, name ASC;";
        private const string SELECT_BY_PARENT_AND_NAME =
            "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE parent = @parent AND name = @name LIMIT 1;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly int OWNER_TYPE_CODE;
        private readonly IDataSource _source;
        public ScriptDataMapper(IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(ScriptRecord));
            OWNER_TYPE_CODE = _source.Model.GetTypeCode(typeof(InfoBaseRecord));

            ConfigureDatabase();
        }
        private void ConfigureDatabase()
        {
            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = CREATE_TABLE_SCRIPT;

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        public void Insert(EntityObject entity)
        {
            if (entity is not ScriptRecord record)
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
                    command.Parameters.AddWithValue("owner", record.Owner.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("parent", record.Parent.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("is_folder", record.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("script", record.Script);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not ScriptRecord record)
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
                    command.Parameters.AddWithValue("owner", record.Owner.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("parent", record.Parent.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("is_folder", record.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("script", record.Script);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            if (entity.IsEmpty || entity.IsUndefined)
            {
                return;
            }

            DeleteRecursively(entity); // database, script folder or record reference
        }
        private void DeleteScript(Entity entity)
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
        private void DeleteRecursively(Entity parent)
        {
            List<ScriptRecord> children = Select(parent) as List<ScriptRecord>;

            foreach (ScriptRecord child in children)
            {
                if (child.IsFolder)
                {
                    DeleteRecursively(child.GetEntity());
                }
                else
                {
                    DeleteScript(child.GetEntity());
                }
            }

            DeleteScript(parent);
        }
        public EntityObject Select(string name)
        {
            string[] segments = name.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (segments is null || segments.Length < 2)
            {
                return null; // NOT FOUND
            }

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(segments[0]);

            if (database is null)
            {
                return null; // NOT FOUND
            }

            string segment;
            ScriptRecord current = null;
            Entity parent = database.GetEntity();

            for (int i = 1; i < segments.Length; i++)
            {
                segment = segments[i];
                current = Select(parent, segment);

                if (current is null)
                {
                    return null; // NOT FOUND
                }

                parent = current.GetEntity();
            }

            return current;
        }
        private ScriptRecord Select(Entity parent, string name)
        {
            ScriptRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Parameters.AddWithValue("name", name);

                    if (parent.TypeCode == MY_TYPE_CODE) // script
                    {
                        command.CommandText = SELECT_BY_PARENT_AND_NAME;
                        command.Parameters.AddWithValue("parent", parent.Identity.ToString().ToLowerInvariant());
                    }
                    else // OWNER_TYPE_CODE == database
                    {
                        command.CommandText = SELECT_BY_OWNER_AND_NAME;
                        command.Parameters.AddWithValue("owner", parent.Identity.ToString().ToLowerInvariant());
                    }

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new ScriptRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Owner = new Entity(OWNER_TYPE_CODE, new Guid(reader.GetString(1))),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(2))),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4),
                                Script = reader.GetString(5)
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
            ScriptRecord record = null;

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
                            record = new ScriptRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Owner = new Entity(OWNER_TYPE_CODE, new Guid(reader.GetString(1))),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(2))),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4),
                                Script = reader.GetString(5)
                            };
                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public IEnumerable Select(Entity parent)
        {
            List<ScriptRecord> list = new();

            if (parent.IsEmpty || parent.IsUndefined)
            {
                return list; // database context is absent
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    if (parent.TypeCode == MY_TYPE_CODE) // script
                    {
                        command.CommandText = SELECT_BY_PARENT;
                        command.Parameters.AddWithValue("parent", parent.Identity.ToString().ToLowerInvariant());
                    }
                    else // OWNER_TYPE_CODE == database
                    {
                        command.CommandText = SELECT_BY_OWNER;
                        command.Parameters.AddWithValue("owner", parent.Identity.ToString().ToLowerInvariant());
                    }

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScriptRecord record = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Owner = new Entity(OWNER_TYPE_CODE, new Guid(reader.GetString(1))),
                                Parent = new Entity(MY_TYPE_CODE, new Guid(reader.GetString(2))),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4),
                                Script = reader.GetString(5)
                            };

                            record.MarkAsOriginal();

                            list.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public IEnumerable Select()
        {
            throw new NotImplementedException();
        }
        public EntityObject Select(int code)
        {
            throw new NotImplementedException();
        }
    }
}