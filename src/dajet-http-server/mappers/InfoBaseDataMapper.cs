using DaJet.Http.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Http.DataMappers
{
    public sealed class InfoBaseDataMapper
    {
        private const string DATABASE_FILE_NAME = "dajet-http-server.db";
        private const string CREATE_INFOBASE_TABLE_SCRIPT = "CREATE TABLE IF NOT EXISTS " +
            "infobases (name TEXT NOT NULL, description TEXT NOT NULL, provider TEXT NOT NULL, dbconnect TEXT NOT NULL, " +
            "PRIMARY KEY (name)) WITHOUT ROWID;";
        private const string SELECT_ALL_SCRIPT = "SELECT name, description, provider, dbconnect FROM infobases ORDER BY name ASC;";
        private const string SELECT_BY_NAME_SCRIPT = "SELECT name, description, provider, dbconnect FROM infobases WHERE name = @name;";
        private const string INSERT_SCRIPT = "INSERT INTO infobases (name, description, provider, dbconnect) VALUES (@name, @description, @provider, @dbconnect);";
        private const string UPDATE_SCRIPT = "UPDATE infobases SET description = @description, provider = @provider, dbconnect = @dbconnect WHERE name = @name;";
        private const string DELETE_SCRIPT = "DELETE FROM infobases WHERE name = @name;";

        private readonly string _connectionString;
        public InfoBaseDataMapper()
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

        #region "INFOBASE CRUD COMMANDS"

        public List<InfoBaseModel> Select()
        {
            List<InfoBaseModel> list = new();

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
                            InfoBaseModel item = new()
                            {
                                Name = reader.GetString(0),
                                Description = reader.GetString(1),
                                DatabaseProvider = reader.GetString(2),
                                ConnectionString = reader.GetString(3)
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public InfoBaseModel Select(string name)
        {
            InfoBaseModel entity = null;

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
                            entity = new InfoBaseModel()
                            {
                                Name = reader.GetString(0),
                                Description = reader.GetString(1),
                                DatabaseProvider = reader.GetString(2),
                                ConnectionString = reader.GetString(3)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            return entity;
        }
        public bool Insert(InfoBaseModel entity)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_SCRIPT;

                    command.Parameters.AddWithValue("name", entity.Name);
                    command.Parameters.AddWithValue("description", entity.Description);
                    command.Parameters.AddWithValue("provider", entity.DatabaseProvider);
                    command.Parameters.AddWithValue("dbconnect", entity.ConnectionString);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Update(InfoBaseModel entity)
        {
            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_SCRIPT;

                    command.Parameters.AddWithValue("name", entity.Name);
                    command.Parameters.AddWithValue("description", entity.Description);
                    command.Parameters.AddWithValue("provider", entity.DatabaseProvider);
                    command.Parameters.AddWithValue("dbconnect", entity.ConnectionString);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result == 1);
        }
        public bool Delete(InfoBaseModel entity)
        {
            int result;

            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_SCRIPT;

                    command.Parameters.AddWithValue("name", entity.Name);

                    result = command.ExecuteNonQuery();
                }
            }

            return (result > 0);
        }

        #endregion
    }
}