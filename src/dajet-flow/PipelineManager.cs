using DaJet.Flow.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Flow
{
    public interface IPipelineManager
    {
        List<PipelineOptions> Select();
        PipelineOptions Select(Guid uuid);
        bool Insert(in PipelineOptions options);
        bool Update(in PipelineOptions options);
        bool Delete(in PipelineOptions options);
    }
    public sealed class PipelineManager : IPipelineManager
    {
        #region "SQL: CREATE TABLES"

        private const string CREATE_PIPELINE_TABLE_SCRIPT =
            "CREATE TABLE IF NOT EXISTS " +
            "pipelines (uuid TEXT NOT NULL, name TEXT NOT NULL UNIQUE, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";

        private const string CREATE_PIPELINE_BLOCKS_TABLE_SCRIPT =
            "CREATE TABLE IF NOT EXISTS " +
            "pipeline_blocks (uuid TEXT NOT NULL, " +
            "pipeline TEXT NOT NULL, ordinal INTEGER NOT NULL, " +
            "script TEXT NOT NULL, handler TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";

        private const string CREATE_OPTIONS_TABLE_SCRIPT =
            "CREATE TABLE IF NOT EXISTS " +
            "options (owner TEXT NOT NULL, key TEXT NOT NULL, value TEXT NOT NULL, " +
            "PRIMARY KEY (owner, key)) WITHOUT ROWID;";

        #endregion

        #region "SQL: CRUD SCRIPTS"

        private const string SELECT_PIPELINES_SCRIPT = "SELECT uuid, name FROM pipelines ORDER BY name ASC;";
        private const string SELECT_BY_UUID_SCRIPT = "SELECT uuid, name FROM pipelines WHERE uuid = @uuid;";
        private const string SELECT_BY_NAME_SCRIPT = "SELECT uuid, name FROM pipelines WHERE name = @name;";
        private const string INSERT_SCRIPT = "INSERT INTO pipelines (uuid, name) VALUES (@uuid, @name);";
        private const string UPDATE_SCRIPT = "UPDATE pipelines SET name = @name WHERE uuid = @uuid;";
        private const string DELETE_SCRIPT = "DELETE FROM pipelines WHERE uuid = @uuid;";

        private const string SELECT_OPTIONS_SCRIPT =
            "SELECT key, value FROM options WHERE owner = @owner ORDER BY key ASC;";
        private const string INSERT_OPTION_SCRIPT =
            "INSERT INTO options (owner, key, value) VALUES (@owner, @key, @value);";
        private const string DELETE_OPTIONS_SCRIPT =
            "DELETE FROM options WHERE owner = @owner;";

        private const string SELECT_PIPELINE_BLOCKS_SCRIPT =
            "SELECT uuid, ordinal, script, handler FROM pipeline_blocks " +
            "WHERE pipeline = @pipeline ORDER BY ordinal ASC;";
        private const string INSERT_PIPELINE_BLOCK_SCRIPT =
            "INSERT INTO pipeline_blocks (uuid, pipeline, ordinal, script, handler) " +
            "VALUES (@uuid, @pipeline, @ordinal, @script, @handler);";
        private const string DELETE_PIPELINE_BLOCKS_SCRIPT =
            "DELETE FROM pipeline_blocks WHERE pipeline = @pipeline;";

        #endregion

        private readonly string _connectionString;
        public PipelineManager(string connectionString)
        {
            _connectionString = connectionString; InitializeDatabase();
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

        #region "CRUD COMMANDS"

        public List<PipelineOptions> Select()
        {
            List<PipelineOptions> list = new();

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
                            PipelineOptions item = new()
                            {
                                Uuid = new Guid(reader.GetString(0)),
                                Name = reader.GetString(1)
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
                                Name = reader.GetString(1)
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
        private Dictionary<string, string> SelectOptions(Guid owner)
        {
            Dictionary<string, string> options = new();

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
                            options.Add(reader.GetString(0), reader.GetString(1));
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
                                Script = reader.GetString(2),
                                Handler = reader.GetString(3)
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
                        command.Parameters.AddWithValue("key", option.Key);
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
                        command.Parameters.AddWithValue("key", option.Key);
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
                        command.Parameters.AddWithValue("script", block.Script);
                        command.Parameters.AddWithValue("handler", block.Handler);
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

                    result = command.ExecuteNonQuery();
                }
            }

            DeleteOptions(entity.Uuid);
            foreach (PipelineBlock block in entity.Blocks)
            {
                DeleteOptions(block.Uuid);
            }
            DeletePipelineBlocks(entity.Uuid);

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