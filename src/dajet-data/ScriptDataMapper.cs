using DaJet.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Data
{
    public sealed class ScriptDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_INFOBASE_TABLE_SCRIPT = "CREATE TABLE IF NOT EXISTS " +
            "scripts (uuid TEXT NOT NULL, owner TEXT NOT NULL, parent TEXT NOT NULL, " +
            "is_folder INTEGER NOT NULL, name TEXT NOT NULL, script TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_ROOT_SCRIPT = "SELECT uuid, owner, parent, is_folder, name FROM scripts WHERE owner = @owner AND parent = @parent ORDER BY is_folder ASC, name ASC;";
        private const string SELECT_NODE_SCRIPT = "SELECT uuid, owner, parent, is_folder, name FROM scripts WHERE parent = @parent ORDER BY is_folder ASC, name ASC;";
        private const string SELECT_INFO_SCRIPT = "SELECT uuid, owner, parent, is_folder, name FROM scripts WHERE uuid = @uuid;";
        private const string SELECT_SCRIPT = "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE uuid = @uuid;";
        private const string INSERT_SCRIPT = "INSERT INTO scripts (uuid, owner, parent, is_folder, name, script) VALUES (@uuid, @owner, @parent, @is_folder, @name, @script);";
        private const string UPDATE_SCRIPT = "UPDATE scripts SET owner = @owner, parent = @parent, is_folder = @is_folder, name = @name, script = @script WHERE uuid = @uuid;";
        private const string UPDATE_NAME_SCRIPT = "UPDATE scripts SET name = @name WHERE uuid = @uuid;";
        private const string DELETE_SCRIPT = "DELETE FROM scripts WHERE uuid = @uuid;";
        #endregion

        private readonly string _connectionString;
        public ScriptDataMapper(string connectionString)
        {
            _connectionString = connectionString;

            InitializeDatabase();
        }
        private void InitializeDatabase()
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = CREATE_INFOBASE_TABLE_SCRIPT;

                    _ = command.ExecuteNonQuery();
                }
            }
        }

        #region "CRUD COMMANDS"
        public List<ScriptRecord> Select(Guid database)
        {
            List<ScriptRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_ROOT_SCRIPT;

                    command.Parameters.AddWithValue("owner", database.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", Guid.Empty.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScriptRecord item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = new Guid(reader.GetString(1)),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4),
                                Script = string.Empty
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public List<ScriptRecord> Select(Guid database, Guid parent)
        {
            List<ScriptRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_ROOT_SCRIPT;

                    command.Parameters.AddWithValue("owner", database.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", parent.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScriptRecord item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = new Guid(reader.GetString(1)),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4),
                                Script = string.Empty
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public List<ScriptRecord> Select(ScriptRecord parent)
        {
            List<ScriptRecord> list = new();

            if (parent is null) { return list; }

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_NODE_SCRIPT;

                    command.Parameters.AddWithValue("parent", parent.Uuid.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScriptRecord item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = new Guid(reader.GetString(1)),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4),
                                Script = string.Empty
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public ScriptRecord SelectScript(Guid uuid)
        {
            ScriptRecord script = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_INFO_SCRIPT;

                    command.Parameters.AddWithValue("uuid", uuid.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            script = new ScriptRecord()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = new Guid(reader.GetString(1)),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt64(3) == 1L),
                                Name = reader.GetString(4)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            return script;
        }
        public bool TrySelect(Guid uuid, out ScriptRecord script)
        {
            script = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", uuid.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            script = new ScriptRecord()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = new Guid(reader.GetString(1)),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt32(3) == 1),
                                Name = reader.GetString(4),
                                Script = reader.GetString(5)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            return (script != null);
        }
        public bool Insert(ScriptRecord script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString().ToLower());
                    command.Parameters.AddWithValue("owner", script.Owner.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", script.Parent.ToString().ToLower());
                    command.Parameters.AddWithValue("is_folder", script.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("name", script.Name);
                    command.Parameters.AddWithValue("script", script.Script);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Update(ScriptRecord script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString());
                    command.Parameters.AddWithValue("owner", script.Owner.ToString());
                    command.Parameters.AddWithValue("parent", script.Parent.ToString());
                    command.Parameters.AddWithValue("is_folder", script.IsFolder ? 1L : 0L);
                    command.Parameters.AddWithValue("name", script.Name);
                    command.Parameters.AddWithValue("script", script.Script);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool UpdateName(ScriptRecord script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_NAME_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString().ToLower());
                    command.Parameters.AddWithValue("name", script.Name);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Delete(ScriptRecord script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            return (result > 0);
        }
        public void DeleteScriptFolder(in ScriptRecord script)
        {
            List<ScriptRecord> children = Select(script);

            foreach (ScriptRecord child in children)
            {
                if (child.IsFolder)
                {
                    DeleteScriptFolder(child);
                }
                else
                {
                    Delete(child);
                }
            }

            Delete(script);
        }
        #endregion

        public void GetScriptChildren(ScriptRecord parent)
        {
            List<ScriptRecord> list = Select(parent);

            if (list.Count == 0)
            {
                return;
            }

            parent.Children.AddRange(list);

            foreach (ScriptRecord child in parent.Children)
            {
                GetScriptChildren(child);
            }
        }
        public ScriptRecord SelectScriptByPath(Guid database, string path)
        {
            string[] segments = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            int counter = 0;
            ScriptRecord current = null;
            List<ScriptRecord> list = Select(database);

            foreach (string segment in segments)
            {
                current = list.Where(item => item.Name == segment).FirstOrDefault();

                if (current == null) { break; }

                counter++;

                if (counter < segments.Length)
                {
                    list = Select(current);
                }
            }

            if (counter == segments.Length && current != null)
            {
                if (TrySelect(current.Uuid, out ScriptRecord script))
                {
                    return script;
                }
                else
                {
                    return null;
                }
            }

            return null; // not found
        }
    }
}