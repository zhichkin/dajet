using DaJet.Flow.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Flow
{
    public interface IPipelineOptionsProvider
    {
        List<PipelineInfo> Select();
        PipelineOptions Select(Guid uuid);
        bool Insert(in PipelineOptions options);
        bool Update(in PipelineOptions options);
        bool Delete(in PipelineOptions options);
    }
    public sealed class PipelineOptionsProvider : IPipelineOptionsProvider
    {
        private readonly string _connectionString;
        public PipelineOptionsProvider(string connectionString)
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
                    command.CommandText = CREATE_PIPELINE_TABLE_SCRIPT;
                    _ = command.ExecuteNonQuery();

                    command.CommandText = CREATE_PIPELINE_BLOCKS_TABLE_SCRIPT;
                    _ = command.ExecuteNonQuery();

                    command.CommandText = CREATE_OPTIONS_TABLE_SCRIPT;
                    _ = command.ExecuteNonQuery();
                }
            }
        }

        #region "SQL: CREATE TABLES"

        private const string CREATE_PIPELINE_TABLE_SCRIPT =
            "CREATE TABLE IF NOT EXISTS " +
            "pipelines (uuid TEXT NOT NULL, name TEXT NOT NULL UNIQUE, activation INTEGER NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";

        private const string CREATE_PIPELINE_BLOCKS_TABLE_SCRIPT =
            "CREATE TABLE IF NOT EXISTS " +
            "blocks (uuid TEXT NOT NULL, " +
            "pipeline TEXT NOT NULL, ordinal INTEGER NOT NULL, " +
            "handler TEXT NOT NULL, message TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";

        private const string CREATE_OPTIONS_TABLE_SCRIPT =
            "CREATE TABLE IF NOT EXISTS " +
            "options (owner TEXT NOT NULL, name TEXT NOT NULL, type TEXT NOT NULL, value TEXT NOT NULL, " +
            "PRIMARY KEY (owner, name)) WITHOUT ROWID;";

        #endregion

        #region "SQL: CRUD SCRIPTS"

        private const string SELECT_PIPELINES_SCRIPT = "SELECT uuid, name, activation FROM pipelines ORDER BY name ASC;";
        private const string SELECT_BY_UUID_SCRIPT = "SELECT uuid, name, activation FROM pipelines WHERE uuid = @uuid;";
        private const string INSERT_SCRIPT = "INSERT INTO pipelines (uuid, name, activation) VALUES (@uuid, @name, @activation);";
        private const string UPDATE_SCRIPT = "UPDATE pipelines SET name = @name, activation = @activation WHERE uuid = @uuid;";
        private const string DELETE_SCRIPT = "DELETE FROM pipelines WHERE uuid = @uuid;";

        private const string SELECT_OPTIONS_SCRIPT =
            "SELECT name, type, value FROM options WHERE owner = @owner ORDER BY name ASC;";
        private const string INSERT_OPTION_SCRIPT =
            "INSERT INTO options (owner, name, type, value) VALUES (@owner, @name, @type, @value);";
        private const string DELETE_OPTIONS_SCRIPT =
            "DELETE FROM options WHERE owner = @owner;";

        private const string SELECT_PIPELINE_BLOCKS_SCRIPT =
            "SELECT uuid, ordinal, handler, message FROM blocks " +
            "WHERE pipeline = @pipeline ORDER BY ordinal ASC;";
        private const string INSERT_PIPELINE_BLOCK_SCRIPT =
            "INSERT INTO blocks (uuid, pipeline, ordinal, handler, message) " +
            "VALUES (@uuid, @pipeline, @ordinal, @handler, @message);";
        private const string DELETE_PIPELINE_BLOCKS_SCRIPT =
            "DELETE FROM blocks WHERE pipeline = @pipeline;";

        #endregion

        #region "SQL: CRUD COMMANDS"

        public List<PipelineInfo> Select()
        {
            List<PipelineInfo> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_PIPELINES_SCRIPT;

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PipelineInfo item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Activation = (ActivationMode)reader.GetInt32(2)
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public PipelineOptions Select(Guid uuid)
        {
            PipelineOptions options = null;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_UUID_SCRIPT;

                    command.Parameters.AddWithValue("uuid", uuid.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            options = new PipelineOptions()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Activation = (ActivationMode)reader.GetInt32(2)
                            };
                        }
                        reader.Close();
                    }
                }
            }

            if (options is not null)
            {
                options.Blocks = SelectPipelineBlocks(options.Uuid);
                options.Options = SelectOptions(options.Uuid);
            }

            return options;
        }
        private List<OptionItem> SelectOptions(Guid owner)
        {
            List<OptionItem> options = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_OPTIONS_SCRIPT;

                    command.Parameters.AddWithValue("owner", owner.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            options.Add(new OptionItem()
                            {
                                Name = reader.GetString(0),
                                Type = reader.GetString(1),
                                Value = reader.GetString(2)
                            });
                        }
                        reader.Close();
                    }
                }
            }

            return options;
        }
        private List<PipelineBlock> SelectPipelineBlocks(Guid pipeline)
        {
            List<PipelineBlock> list = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_PIPELINE_BLOCKS_SCRIPT;

                    command.Parameters.AddWithValue("pipeline", pipeline.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PipelineBlock item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Ordinal = reader.GetInt32(1),
                                Handler = reader.GetString(2),
                                Message = reader.GetString(3)
                            };
                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            foreach (PipelineBlock block in list)
            {
                block.Options = SelectOptions(block.Uuid);
            }

            return list;
        }

        public bool Insert(in PipelineOptions entity)
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
                    command.Parameters.AddWithValue("activation", (int)entity.Activation);

                    result = command.ExecuteNonQuery();
                }
            }

            InsertOptions(entity);
            InsertPipelineBlocks(entity);

            return (result == 1);
        }
        private void InsertOptions(PipelineOptions entity)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_OPTION_SCRIPT;

                    foreach (var option in entity.Options)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("owner", entity.Uuid.ToString().ToLower());
                        command.Parameters.AddWithValue("name", option.Name);
                        command.Parameters.AddWithValue("type", option.Type);
                        command.Parameters.AddWithValue("value", option.Value);
                        _ = command.ExecuteNonQuery();
                    }
                }
            }
        }
        private void InsertOptions(PipelineBlock entity)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_OPTION_SCRIPT;

                    foreach (var option in entity.Options)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("owner", entity.Uuid.ToString().ToLower());
                        command.Parameters.AddWithValue("name", option.Name);
                        command.Parameters.AddWithValue("type", option.Type);
                        command.Parameters.AddWithValue("value", option.Value);
                        _ = command.ExecuteNonQuery();
                    }
                }
            }
        }
        private void InsertPipelineBlocks(PipelineOptions entity)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_PIPELINE_BLOCK_SCRIPT;

                    int ordinal = 0;

                    foreach (PipelineBlock block in entity.Blocks)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("uuid", block.Uuid.ToString().ToLower());
                        command.Parameters.AddWithValue("pipeline", entity.Uuid.ToString().ToLower());
                        command.Parameters.AddWithValue("ordinal", ordinal++);
                        command.Parameters.AddWithValue("handler", block.Handler);
                        command.Parameters.AddWithValue("message", block.Message);
                        _ = command.ExecuteNonQuery();
                    }
                }
            }

            foreach (PipelineBlock block in entity.Blocks)
            {
                InsertOptions(block);
            }
        }

        public bool Update(in PipelineOptions entity)
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
                    command.Parameters.AddWithValue("activation", (int)entity.Activation);

                    result = command.ExecuteNonQuery();
                }
            }

            PipelineOptions current = Select(entity.Uuid);
            DeleteOptions(current.Uuid);
            foreach (PipelineBlock block in current.Blocks)
            {
                DeleteOptions(block.Uuid);
            }
            DeletePipelineBlocks(current.Uuid);

            InsertOptions(entity);
            InsertPipelineBlocks(entity);

            return (result == 1);
        }

        public bool Delete(in PipelineOptions entity)
        {
            int result;

            DeleteOptions(entity.Uuid);

            foreach (PipelineBlock block in entity.Blocks)
            {
                DeleteOptions(block.Uuid);
            }

            DeletePipelineBlocks(entity.Uuid);

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
        private void DeleteOptions(Guid owner)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_OPTIONS_SCRIPT;

                    command.Parameters.AddWithValue("owner", owner.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        private void DeletePipelineBlocks(Guid pipeline)
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_PIPELINE_BLOCKS_SCRIPT;

                    command.Parameters.AddWithValue("pipeline", pipeline.ToString().ToLower());

                    int result = command.ExecuteNonQuery();
                }
            }
        }

        #endregion
    }
}