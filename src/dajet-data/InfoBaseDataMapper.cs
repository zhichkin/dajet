using DaJet.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Data
{
    public sealed class InfoBaseDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_INFOBASE_TABLE_SCRIPT = "CREATE TABLE IF NOT EXISTS " +
            "infobases (uuid TEXT NOT NULL, name TEXT NOT NULL UNIQUE, description TEXT NOT NULL, " +
            "use_extensions INTEGER NOT NULL, provider TEXT NOT NULL, dbconnect TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_ALL_SCRIPT =
            "SELECT uuid, name, description, use_extensions, provider, dbconnect FROM infobases ORDER BY name ASC;";
        private const string SELECT_SCRIPT =
            "SELECT uuid, name, description, use_extensions, provider, dbconnect FROM infobases WHERE uuid = @uuid;";
        private const string SELECT_BY_NAME_SCRIPT =
            "SELECT uuid, name, description, use_extensions, provider, dbconnect FROM infobases WHERE name = @name;";
        private const string INSERT_SCRIPT =
            "INSERT INTO infobases (uuid, name, description, use_extensions, provider, dbconnect) " +
            "VALUES (@uuid, @name, @description, @use_extensions, @provider, @dbconnect);";
        private const string UPDATE_SCRIPT =
            "UPDATE infobases SET name = @name, description = @description, " +
            "use_extensions = @use_extensions, provider = @provider, dbconnect = @dbconnect " +
            "WHERE uuid = @uuid;";
        private const string DELETE_SCRIPT = "DELETE FROM infobases WHERE uuid = @uuid;";
        #endregion

        private readonly string _connectionString;
        public InfoBaseDataMapper(string connectionString)
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
        public List<InfoBaseRecord> Select()
        {
            List<InfoBaseRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_ALL_SCRIPT;

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            InfoBaseRecord item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                UseExtensions = (reader.GetInt64(3) == 1L),
                                DatabaseProvider = reader.GetString(4),
                                ConnectionString = reader.GetString(5)
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public InfoBaseRecord Select(Guid uuid)
        {
            InfoBaseRecord entity = null;

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
                            entity = new InfoBaseRecord()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                UseExtensions = (reader.GetInt64(3) == 1L),
                                DatabaseProvider = reader.GetString(4),
                                ConnectionString = reader.GetString(5)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            return entity;
        }
        public InfoBaseRecord Select(string name)
        {
            InfoBaseRecord entity = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_NAME_SCRIPT;

                    command.Parameters.AddWithValue("name", name);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            entity = new InfoBaseRecord()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                UseExtensions = (reader.GetInt64(3) == 1L),
                                DatabaseProvider = reader.GetString(4),
                                ConnectionString = reader.GetString(5)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            return entity;
        }
        public bool Insert(InfoBaseRecord entity)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", entity.Uuid.ToString().ToLower());
                    command.Parameters.AddWithValue("name", entity.Name);
                    command.Parameters.AddWithValue("description", entity.Description);
                    command.Parameters.AddWithValue("use_extensions", entity.UseExtensions);
                    command.Parameters.AddWithValue("provider", entity.DatabaseProvider);
                    command.Parameters.AddWithValue("dbconnect", entity.ConnectionString);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Update(InfoBaseRecord entity)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", entity.Uuid.ToString().ToLower());
                    command.Parameters.AddWithValue("name", entity.Name);
                    command.Parameters.AddWithValue("description", entity.Description);
                    command.Parameters.AddWithValue("use_extensions", entity.UseExtensions);
                    command.Parameters.AddWithValue("provider", entity.DatabaseProvider);
                    command.Parameters.AddWithValue("dbconnect", entity.ConnectionString);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Delete(InfoBaseRecord entity)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", entity.Uuid.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            return (result > 0);
        }
        #endregion
    }
}