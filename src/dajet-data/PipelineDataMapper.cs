using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class PipelineDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "pipelines (uuid TEXT NOT NULL, name TEXT NOT NULL, activation INTEGER NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_ALL =
            "SELECT uuid, name, activation FROM pipelines ORDER BY name ASC;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, name, activation FROM pipelines WHERE uuid = @uuid;";
        private const string INSERT_COMMAND =
            "INSERT INTO pipelines (uuid, name, activation) VALUES (@uuid, @name, @activation);";
        private const string UPDATE_COMMAND =
            "UPDATE pipelines SET name = @name, activation = @activation WHERE uuid = @uuid;";
        private const string DELETE_COMMAND =
            "DELETE FROM pipelines WHERE uuid = @uuid;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly string _connectionString;
        private readonly IDomainModel _domain;
        public PipelineDataMapper(IDomainModel domain, string connectionString)
        {
            _connectionString = connectionString;

            ConfigureDatabase();

            _domain = domain;

            MY_TYPE_CODE = _domain.GetTypeCode(typeof(PipelineRecord));
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
            if (entity is not PipelineRecord record)
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
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("activation", record.Activation == PipelineMode.Auto ? 0L : 1L);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not PipelineRecord record)
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
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("activation", record.Activation == PipelineMode.Auto ? 0L : 1L);

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
        public IEnumerable Select()
        {
            List<PipelineRecord> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_ALL;

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PipelineRecord record = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Activation = (PipelineMode)reader.GetInt64(2)
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
        public EntityObject Select(Guid identity)
        {
            PipelineRecord record = null;

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
                            record = new PipelineRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Activation = (PipelineMode)reader.GetInt64(2)
                            };

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
            throw new NotImplementedException();
        }
    }
}