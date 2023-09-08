using DaJet.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Data
{
    public sealed class TreeNodeDataMapper : IDataMapper
    {
        #region "SQL SCRIPTS"
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "maintree (uuid TEXT NOT NULL, parent TEXT NOT NULL, name TEXT NOT NULL, is_folder INTEGER NOT NULL, " +
            "entity_type INTEGER NOT NULL, entity_uuid TEXT NOT NULL, PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string SELECT_ALL =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree ORDER BY name ASC;";
        private const string SELECT_BY_UUID =
            "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE uuid = @uuid;";
        private const string INSERT_COMMAND =
            "INSERT INTO maintree (uuid, parent, name, is_folder, entity_type, entity_uuid) " +
            "VALUES (@uuid, @parent, @name, @is_folder, @entity_type, @entity_uuid);";
        private const string UPDATE_COMMAND =
            "UPDATE maintree SET " +
            "parent = @parent, name = @name, is_folder = @is_folder, " +
            "entity_type = @entity_type, entity_uuid = @entity_uuid " +
            "WHERE uuid = @uuid;";
        private const string DELETE_COMMAND = "DELETE FROM maintree WHERE uuid = @uuid;";
        #endregion

        private readonly int MY_TYPE_CODE = -10;
        private readonly string _connectionString;
        private readonly IDataSource _source;
        public TreeNodeDataMapper(IDataSource source, string connectionString)
        {
            _source = source;
            _connectionString = connectionString;
        }
        public void Insert(Persistent entity)
        {
            if (entity is not TreeNodeRecord record)
            {
                throw new ArgumentException(null, nameof(entity));
            }

            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", record.Parent.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("is_folder", record.IsFolder);
                    command.Parameters.AddWithValue("entity_type", MY_TYPE_CODE);
                    command.Parameters.AddWithValue("entity_uuid", record.Value.Identity.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            //return (result == 1);
        }
        public void Update(Persistent entity)
        {
            if (entity is not TreeNodeRecord record)
            {
                throw new ArgumentException(null, nameof(entity));
            }

            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());
                    command.Parameters.AddWithValue("parent", record.Parent.ToString().ToLower());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("is_folder", record.IsFolder);
                    command.Parameters.AddWithValue("entity_type", MY_TYPE_CODE);
                    command.Parameters.AddWithValue("entity_uuid", record.Value.Identity.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            //return (result == 1);
        }
        public void Delete(Persistent entity)
        {
            if (entity is not TreeNodeRecord record)
            {
                throw new ArgumentException(null, nameof(entity));
            }

            int result;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            //return (result > 0);
        }
        public void Select(Persistent entity)
        {
            if (entity is not TreeNodeRecord record)
            {
                throw new ArgumentException(null, nameof(entity));
            }

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_UUID;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLower());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record.Parent = new TreeNodeRecord(_source);
                            record.SetVirtualState(new Guid(reader.GetString(1)));
                            
                            record.SetLoadingState();
                            record.Name = reader.GetString(2);
                            record.IsFolder = (reader.GetInt64(3) == 1L);
                            record.SetOriginalState();

                            //long code = reader.GetInt64(4);
                            //Guid uuid = new(reader.GetString(5));
                            //record.Value = _domain.Create((int)code, uuid);
                        }
                        reader.Close();
                    }
                }
            }
        }
    }
}