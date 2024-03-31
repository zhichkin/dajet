using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class InfoBaseDataMapper : IDataMapper
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
        private const string DELETE_COMMAND = "DELETE FROM infobases WHERE uuid = @uuid;";
        private const string DELETE_SCRIPTS = "DELETE FROM scripts WHERE owner = @owner;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly IDataSource _source;
        public InfoBaseDataMapper(IDataSource source)
        {
            _source = source;

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(InfoBaseRecord));

            ConfigureDatabase();
        }
        private void ConfigureDatabase()
        {
            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = CREATE_INFOBASE_TABLE_SCRIPT;

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        public void Insert(EntityObject entity)
        {
            if (entity is not InfoBaseRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("description", record.Description);
                    command.Parameters.AddWithValue("use_extensions", record.UseExtensions ? 1L : 0L);
                    command.Parameters.AddWithValue("provider", record.DatabaseProvider);
                    command.Parameters.AddWithValue("dbconnect", record.ConnectionString);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not InfoBaseRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_SCRIPT;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("description", record.Description);
                    command.Parameters.AddWithValue("use_extensions", record.UseExtensions ? 1L : 0L);
                    command.Parameters.AddWithValue("provider", record.DatabaseProvider);
                    command.Parameters.AddWithValue("dbconnect", record.ConnectionString);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            int result = 0;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Connection = connection;
                        command.Transaction = transaction;

                        command.CommandText = DELETE_SCRIPTS;
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("owner", entity.Identity.ToString().ToLower());
                        result += command.ExecuteNonQuery();

                        command.CommandText = DELETE_COMMAND;
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());
                        result += command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }
        public EntityObject Select(int code) { throw new NotImplementedException(); }
        public EntityObject Select(Entity owner, string name) { throw new NotImplementedException(); }
        public EntityObject Select(string name)
        {
            InfoBaseRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
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
                            record = new InfoBaseRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                UseExtensions = (reader.GetInt64(3) == 1L),
                                DatabaseProvider = reader.GetString(4),
                                ConnectionString = reader.GetString(5)
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
            InfoBaseRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_SCRIPT;

                    command.Parameters.AddWithValue("uuid", idenity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new InfoBaseRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                UseExtensions = (reader.GetInt64(3) == 1L),
                                DatabaseProvider = reader.GetString(4),
                                ConnectionString = reader.GetString(5)
                            };
                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public IEnumerable Select()
        {
            List<InfoBaseRecord> list = new();

            using (SqliteConnection connection = new(_source.ConnectionString))
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
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                UseExtensions = (reader.GetInt64(3) == 1L),
                                DatabaseProvider = reader.GetString(4),
                                ConnectionString = reader.GetString(5)
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
        public IEnumerable Select(Entity owner)
        {
            throw new NotImplementedException();
        }
    }
}