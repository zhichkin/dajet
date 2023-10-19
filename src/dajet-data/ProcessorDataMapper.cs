using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class ProcessorDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "blocks (uuid TEXT NOT NULL, pipeline TEXT NOT NULL, ordinal INTEGER NOT NULL, " +
            "handler TEXT NOT NULL, message TEXT NOT NULL, PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_BY_OWNER =
            "SELECT uuid, pipeline, ordinal, handler, message FROM blocks WHERE pipeline = @pipeline ORDER BY ordinal ASC;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, pipeline, ordinal, handler, message FROM blocks WHERE uuid = @uuid;";
        private const string INSERT_COMMAND =
            "INSERT INTO blocks (uuid, pipeline, ordinal, handler, message) " +
            "VALUES (@uuid, @pipeline, @ordinal, @handler, @message);";
        private const string UPDATE_COMMAND =
            "UPDATE blocks SET pipeline = @pipeline, ordinal = @ordinal, handler = @handler, message = @message WHERE uuid = @uuid;";
        private const string DELETE_COMMAND =
            "DELETE FROM blocks WHERE uuid = @uuid;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly string _connectionString;
        private readonly IDomainModel _domain;
        public ProcessorDataMapper(IDomainModel domain, string connectionString)
        {
            _connectionString = connectionString;

            ConfigureDatabase();

            _domain = domain;

            MY_TYPE_CODE = _domain.GetTypeCode(typeof(ProcessorRecord));
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
            if (entity is not ProcessorRecord record)
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
                    command.Parameters.AddWithValue("pipeline", record.Pipeline.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("ordinal", record.Ordinal);
                    command.Parameters.AddWithValue("handler", record.Handler);
                    command.Parameters.AddWithValue("message", record.Message);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not ProcessorRecord record)
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
                    command.Parameters.AddWithValue("pipeline", record.Pipeline.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("ordinal", record.Ordinal);
                    command.Parameters.AddWithValue("handler", record.Handler);
                    command.Parameters.AddWithValue("message", record.Message);

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
            ProcessorRecord record = null;

            int ownerCode = _domain.GetTypeCode(typeof(PipelineRecord));

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
                            record = new ProcessorRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Pipeline = new Entity(ownerCode, new Guid(reader.GetString(1))),
                                Ordinal = (int)reader.GetInt64(2),
                                Handler = reader.GetString(3),
                                Message = reader.GetString(4)
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
            List<ProcessorRecord> list = new();

            int ownerCode = _domain.GetTypeCode(typeof(PipelineRecord));

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_OWNER;

                    command.Parameters.AddWithValue("pipeline", owner.Identity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ProcessorRecord record = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Pipeline = new Entity(ownerCode, new Guid(reader.GetString(1))),
                                Ordinal = (int)reader.GetInt64(2),
                                Handler = reader.GetString(3),
                                Message = reader.GetString(4)
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
    }
}