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
        private const string SELECT_BY_NAME =
            "SELECT uuid, name, activation FROM pipelines WHERE name = @name LIMIT 1;";
        private const string INSERT_COMMAND =
            "INSERT INTO pipelines (uuid, name, activation) VALUES (@uuid, @name, @activation);";
        private const string UPDATE_COMMAND =
            "UPDATE pipelines SET name = @name, activation = @activation WHERE uuid = @uuid;";
        
        private const string DELETE_COMMAND =
            "DELETE FROM pipelines WHERE uuid = @uuid;";
        private const string DELETE_BLOCK_OPTIONS =
            "DELETE FROM options WHERE owner_type = @type AND owner_uuid IN (SELECT uuid FROM handlers WHERE pipeline = @uuid);";
        private const string DELETE_PIPELINE_OPTIONS =
            "DELETE FROM options WHERE owner_type = @type AND owner_uuid = @uuid;";
        private const string DELETE_PIPELINE_BLOCKS =
            "DELETE FROM handlers WHERE pipeline = @uuid;";

        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly IDataSource _source;
        public PipelineDataMapper(IDataSource source)
        {
            _source = source;

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(PipelineRecord));

            ConfigureDatabase();
        }
        private void ConfigureDatabase()
        {
            using (SqliteConnection connection = new(_source.ConnectionString))
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

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("activation", record.Activation == ActivationMode.Auto ? 0L : 1L);

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

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("activation", record.Activation == ActivationMode.Auto ? 0L : 1L);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            int result = 0;
            int pipeline = _source.Model.GetTypeCode(typeof(PipelineRecord));
            int processor = _source.Model.GetTypeCode(typeof(HandlerRecord));

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Connection = connection;
                        command.Transaction = transaction;

                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("type", processor);
                        command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());
                        command.CommandText = DELETE_BLOCK_OPTIONS;
                        result += command.ExecuteNonQuery();

                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("type", pipeline);
                        command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());

                        command.CommandText = DELETE_PIPELINE_OPTIONS;
                        result += command.ExecuteNonQuery();

                        command.CommandText = DELETE_PIPELINE_BLOCKS;
                        result += command.ExecuteNonQuery();

                        command.CommandText = DELETE_COMMAND;
                        result += command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }
        public IEnumerable Select()
        {
            List<PipelineRecord> list = new();

            using (SqliteConnection connection = new(_source.ConnectionString))
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
                                Activation = (ActivationMode)reader.GetInt64(2)
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

            using (SqliteConnection connection = new(_source.ConnectionString))
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
                                Activation = (ActivationMode)reader.GetInt64(2)
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
        public EntityObject Select(string name)
        {
            PipelineRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_NAME;

                    command.Parameters.AddWithValue("name", name);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new PipelineRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Activation = (ActivationMode)reader.GetInt64(2)
                            };

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