using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace DaJet.Data
{
    public sealed class HandlerDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "handlers (uuid TEXT NOT NULL, pipeline TEXT NOT NULL, ordinal INTEGER NOT NULL, " +
            "handler TEXT NOT NULL, message TEXT NOT NULL, PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_BY_OWNER =
            "SELECT uuid, pipeline, ordinal, handler, message FROM handlers WHERE pipeline = @pipeline ORDER BY ordinal ASC;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, pipeline, ordinal, handler, message FROM handlers WHERE uuid = @uuid;";
        private const string INSERT_COMMAND =
            "INSERT INTO handlers (uuid, pipeline, ordinal, handler, message) " +
            "VALUES (@uuid, @pipeline, @ordinal, @handler, @message);";
        private const string UPDATE_COMMAND =
            "UPDATE handlers SET pipeline = @pipeline, ordinal = @ordinal, handler = @handler, message = @message WHERE uuid = @uuid;";
        private const string DELETE_COMMAND =
            "DELETE FROM handlers WHERE uuid = @uuid;";
        private const string DELETE_OPTIONS =
            "DELETE FROM options WHERE owner_type = @type AND owner_uuid = @uuid;";
        #endregion

        private readonly int MY_TYPE_CODE;
        private readonly string _connectionString;
        private readonly IDomainModel _domain;
        public HandlerDataMapper(IDomainModel domain, string connectionString)
        {
            _connectionString = connectionString;

            ConfigureDatabase();

            _domain = domain;

            MY_TYPE_CODE = _domain.GetTypeCode(typeof(HandlerRecord));
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
            if (entity is not HandlerRecord record)
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
            if (entity is not HandlerRecord record)
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
            int result = 0;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Connection = connection;
                        command.Transaction = transaction;

                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("type", entity.TypeCode);
                        command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());
                        command.CommandText = DELETE_OPTIONS;
                        result += command.ExecuteNonQuery();

                        command.Parameters.Clear();
                        command.CommandText = DELETE_COMMAND;
                        command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLower());
                        result += command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }
        public EntityObject Select(Guid identity)
        {
            HandlerRecord record = null;

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
                            record = new HandlerRecord()
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
            List<HandlerRecord> list = new();

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
                            HandlerRecord record = new()
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