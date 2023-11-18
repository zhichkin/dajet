using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class OptionDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS options " +
            "(uuid TEXT NOT NULL, owner_type INTEGER NOT NULL, owner_uuid TEXT NOT NULL, " +
            "name TEXT NOT NULL, type TEXT NOT NULL, value TEXT NOT NULL, " +
            "PRIMARY KEY (uuid), UNIQUE (owner_type, owner_uuid, name)) WITHOUT ROWID;";
        private const string SELECT_BY_OWNER =
            "SELECT uuid, owner_type, owner_uuid, name, type, value FROM options " +
            "WHERE owner_type = @owner_type AND owner_uuid = @owner_uuid ORDER BY name ASC;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, owner_type, owner_uuid, name, type, value FROM options WHERE uuid = @uuid;";
        private const string INSERT_COMMAND =
            "INSERT INTO options (uuid, owner_type, owner_uuid, name, type, value) " +
            "VALUES (@uuid, @owner_type, @owner_uuid, @name, @type, @value);";
        private const string UPDATE_COMMAND =
            "UPDATE options SET owner_type = @owner_type, owner_uuid = @owner_uuid, " +
            "name = @name, type = @type, value = @value WHERE uuid = @uuid;";
        private const string DELETE_COMMAND =
            "DELETE FROM options WHERE uuid = @uuid;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly string _connectionString;
        private readonly IDomainModel _domain;
        public OptionDataMapper(IDomainModel domain, string connectionString)
        {
            _connectionString = connectionString;

            ConfigureDatabase();

            _domain = domain;

            MY_TYPE_CODE = _domain.GetTypeCode(typeof(OptionRecord));
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
            if (entity is not OptionRecord record)
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
                    command.Parameters.AddWithValue("owner_type", record.Owner.TypeCode);
                    command.Parameters.AddWithValue("owner_uuid", record.Owner.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("type", record.Type);
                    command.Parameters.AddWithValue("value", record.Value);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not OptionRecord record)
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
                    command.Parameters.AddWithValue("owner_type", record.Owner.TypeCode);
                    command.Parameters.AddWithValue("owner_uuid", record.Owner.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("type", record.Type);
                    command.Parameters.AddWithValue("value", record.Value);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
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
        public EntityObject Select(Guid identity)
        {
            OptionRecord record = null;

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
                            record = new OptionRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(3),
                                Type = reader.GetString(4),
                                Value = reader.GetString(5)
                            };

                            int code = (int)reader.GetInt64(1);
                            Guid uuid = new(reader.GetString(2));
                            record.Owner = new Entity(code, uuid);

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
            List<OptionRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_OWNER;

                    command.Parameters.AddWithValue("owner_type", owner.TypeCode);
                    command.Parameters.AddWithValue("owner_uuid", owner.Identity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OptionRecord record = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(3),
                                Type = reader.GetString(4),
                                Value = reader.GetString(5)
                            };

                            int code = (int)reader.GetInt64(1);
                            Guid uuid = new(reader.GetString(2));
                            record.Owner = new Entity(code, uuid);

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
        public EntityObject Select(string name)
        {
            throw new NotImplementedException();
        }
    }
}