using DaJet.Http.Model;
using Microsoft.Data.Sqlite;
using System;

namespace DaJet.Http.DataMappers
{
    public sealed class ScriptDataMapper
    {
        private const string DATABASE_FILE_NAME = "dajet-http-server.db";
        private const string CREATE_INFOBASE_TABLE_SCRIPT = "CREATE TABLE IF NOT EXISTS " +
            "scripts (uuid TEXT NOT NULL, owner TEXT NOT NULL, parent TEXT NOT NULL, is_folder INTEGER NOT NULL, name TEXT NOT NULL, script TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_ROOT_SCRIPT = "SELECT uuid, owner, parent, is_folder, name FROM scripts WHERE owner = @owner AND parent = @parent ORDER BY is_folder ASC, name ASC;";
        private const string SELECT_NODE_SCRIPT = "SELECT uuid, owner, parent, is_folder, name FROM scripts WHERE parent = @parent ORDER BY is_folder ASC, name ASC;";
        private const string SELECT_INFO_SCRIPT = "SELECT uuid, owner, parent, is_folder, name FROM scripts WHERE uuid = @uuid;";
        private const string SELECT_SCRIPT = "SELECT uuid, owner, parent, is_folder, name, script FROM scripts WHERE uuid = @uuid;";
        private const string INSERT_SCRIPT = "INSERT INTO scripts (uuid, owner, parent, is_folder, name, script) VALUES (@uuid, @owner, @parent, @is_folder, @name, @script);";
        private const string UPDATE_SCRIPT = "UPDATE scripts SET owner = @owner, parent = @parent, is_folder = @is_folder, name = @name, script = @script WHERE uuid = @uuid;";
        private const string UPDATE_NAME_SCRIPT = "UPDATE scripts SET name = @name WHERE uuid = @uuid;";
        private const string DELETE_SCRIPT = "DELETE FROM scripts WHERE uuid = @uuid;";

        private readonly string _connectionString;
        public ScriptDataMapper()
        {
            string databaseFileFullPath = Path.Combine(AppContext.BaseDirectory, DATABASE_FILE_NAME);

            _connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFileFullPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }
            .ToString();

            InitializeDatabase();
        }
        private void InitializeDatabase()
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
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

        public List<ScriptModel> Select(string infobase)
        {
            List<ScriptModel> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_ROOT_SCRIPT;

                    command.Parameters.AddWithValue("owner", infobase);
                    command.Parameters.AddWithValue("parent", Guid.Empty.ToString());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScriptModel item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = reader.GetString(1),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt32(3) == 1),
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
        public List<ScriptModel> Select(ScriptModel parent)
        {
            List<ScriptModel> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_NODE_SCRIPT;

                    command.Parameters.AddWithValue("parent", parent.Uuid.ToString());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ScriptModel item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = reader.GetString(1),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt32(3) == 1),
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
        public ScriptModel SelectScript(Guid uuid)
        {
            ScriptModel script = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_INFO_SCRIPT;

                    command.Parameters.AddWithValue("uuid", uuid.ToString());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            script = new ScriptModel()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = reader.GetString(1),
                                Parent = new Guid(reader.GetString(2)),
                                IsFolder = (reader.GetInt32(3) == 1),
                                Name = reader.GetString(4)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            return script;
        }
        public bool TrySelect(Guid uuid, out ScriptModel script)
        {
            script = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", uuid.ToString());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            script = new ScriptModel()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Owner = reader.GetString(1),
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
        public bool Insert(ScriptModel script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString());
                    command.Parameters.AddWithValue("owner", script.Owner);
                    command.Parameters.AddWithValue("parent", script.Parent.ToString());
                    command.Parameters.AddWithValue("is_folder", script.IsFolder ? 1 : 0);
                    command.Parameters.AddWithValue("name", script.Name);
                    command.Parameters.AddWithValue("script", script.Script);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Update(ScriptModel script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString());
                    command.Parameters.AddWithValue("owner", script.Owner);
                    command.Parameters.AddWithValue("parent", script.Parent.ToString());
                    command.Parameters.AddWithValue("is_folder", script.IsFolder ? 1 : 0);
                    command.Parameters.AddWithValue("name", script.Name);
                    command.Parameters.AddWithValue("script", script.Script);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool UpdateName(ScriptModel script)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_NAME_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString());
                    command.Parameters.AddWithValue("name", script.Name);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Delete(ScriptModel script)
        {
            int result;

            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", script.Uuid.ToString());

                    result = command.ExecuteNonQuery();
                }
            }

            return (result > 0);
        }

        #endregion
    }
}