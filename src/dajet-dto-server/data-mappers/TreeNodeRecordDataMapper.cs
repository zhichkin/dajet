using DaJet.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Dto.Server
{
    public sealed class TreeNodeRecordDataMapper : IDataMapper
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

        private readonly IDomainModel _domain;
        private readonly string _connectionString;
        public TreeNodeRecordDataMapper(IDomainModel domain, string connectionString)
        {
            _domain = domain;
            _connectionString = connectionString;
        }
        public void Insert(IPersistent entity)
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
                    command.Parameters.AddWithValue("entity_type", record.Value.TypeCode);
                    command.Parameters.AddWithValue("entity_uuid", record.Value.Identity.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            //return (result == 1);
        }
        public void Update(IPersistent entity)
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
                    command.Parameters.AddWithValue("entity_type", record.Value.TypeCode);
                    command.Parameters.AddWithValue("entity_uuid", record.Value.Identity.ToString().ToLower());

                    result = command.ExecuteNonQuery();
                }
            }

            //return (result == 1);
        }
        public void Delete(IPersistent entity)
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
        public void Select(IPersistent entity)
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
                            record.Parent = _domain.New<TreeNodeRecord>(new Guid(reader.GetString(1)));
                            record.Name = reader.GetString(2);
                            record.IsFolder = (reader.GetInt64(3) == 1L);

                            long code = reader.GetInt64(4);
                            Guid uuid = new(reader.GetString(5));
                            record.Value = _domain.Create((int)code, uuid);
                        }
                        reader.Close();
                    }
                }
            }
        }
    }
}

//public partial class Entity
//{
//    public sealed class DataMapper : IDataMapper
//    {
//        #region " SQL "
//        private const string SelectCommandText = @"SELECT [namespace], [owner], [parent], [name], [code], [version], [alias], [is_abstract], [is_sealed] FROM [metadata].[entities] WHERE [key] = @key";
//        private const string InsertCommandText =
//            @"DECLARE @result table([version] binary(8)); " +
//            @"INSERT [metadata].[entities] ([key], [namespace], [owner], [parent], [name], [code], [alias], [is_abstract], [is_sealed]) " +
//            @"OUTPUT inserted.[version] INTO @result " +
//            @"VALUES (@key, @namespace, @owner, @parent, @name, @code, @alias, @is_abstract, @is_sealed); " +
//            @"IF @@ROWCOUNT > 0 SELECT [version] FROM @result;";
//        private const string UpdateCommandText =
//            @"DECLARE @rows_affected int; DECLARE @result table([version] binary(8)); " +
//            @"UPDATE [metadata].[entities] SET [namespace] = @namespace, [owner] = @owner, [parent] = @parent, " +
//            @"[name] = @name, [code] = @code, [alias] = @alias, [is_abstract] = @is_abstract, [is_sealed] = @is_sealed " +
//            @"OUTPUT inserted.[version] INTO @result" +
//            @" WHERE [key] = @key AND [version] = @version; " +
//            @"SET @rows_affected = @@ROWCOUNT; " +
//            @"IF (@rows_affected = 0) " +
//            @"BEGIN " +
//            @"  INSERT @result ([version]) SELECT [version] FROM [metadata].[entities] WHERE [key] = @key; " +
//            @"END " +
//            @"SELECT @rows_affected, [version] FROM @result;";
//        private const string DeleteCommandText =
//            @"DELETE [metadata].[entities] WHERE [key] = @key " +
//            @"   AND ([version] = @version OR @version = 0x00000000); " + // taking into account deletion of the entities having virtual state
//            @"SELECT @@ROWCOUNT;";
//        #endregion
//        private readonly string ConnectionString;
//        private readonly IReferenceObjectFactory Factory;
//        private DataMapper() { }
//        public DataMapper(string connectionString, IReferenceObjectFactory factory)
//        {
//            ConnectionString = connectionString;
//            Factory = factory;
//        }
//        void IDataMapper.Select(IPersistent entity)
//        {
//            Entity e = (Entity)entity;
//            bool ok = false;
//            using (SqlConnection connection = new SqlConnection(ConnectionString))
//            {
//                connection.Open();
//                SqlCommand command  = connection.CreateCommand();
//                command.CommandType = CommandType.Text;
//                command.CommandText = SelectCommandText;
//                SqlParameter parameter = null;
//                parameter = new SqlParameter("key", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.identity;
//                command.Parameters.Add(parameter);
//                SqlDataReader reader = command.ExecuteReader();
//                if (reader.Read())
//                {
//                    Guid guid;
//                    e._namespace = Factory.New<Namespace>((Guid)reader[0]);
//                    guid = (Guid)reader[1];
//                    e.owner = (guid == Guid.Empty) ? null : Factory.New<Entity>(guid);
//                    guid = (Guid)reader[2];
//                    e.parent = (guid == Guid.Empty) ? null : Factory.New<Entity>(guid);
//                    e.name = (string)reader[3];
//                    e.code = (int)reader[4];
//                    e.version = (byte[])reader[5];
//                    e.alias = reader.GetString(6);
//                    e.isAbstract = reader.GetBoolean(7);
//                    e.isSealed = reader.GetBoolean(8);
//                    ok = true;
//                }
//                reader.Close(); connection.Close();
//            }
//            if (!ok) throw new ApplicationException("Error executing select command.");
//        }
//        void IDataMapper.Insert(IPersistent entity)
//        {
//            Entity e = (Entity)entity;
//            bool ok = false;
//            using (SqlConnection connection = new SqlConnection(ConnectionString))
//            {
//                connection.Open();
//                SqlCommand command  = connection.CreateCommand();
//                command.CommandType = CommandType.Text;
//                command.CommandText = InsertCommandText;
//                SqlParameter parameter = null;
//                parameter = new SqlParameter("key", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("namespace", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e._namespace == null) ? Guid.Empty : e._namespace.Identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("owner", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.owner == null) ? Guid.Empty : e.owner.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("parent", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.parent == null) ? Guid.Empty : e.parent.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("name", SqlDbType.NVarChar);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.name == null) ? string.Empty : e.name;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("code", SqlDbType.Int);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.code;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("alias", SqlDbType.NVarChar);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.alias == null) ? string.Empty : e.alias;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("is_abstract", SqlDbType.Bit);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.isAbstract;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("is_sealed", SqlDbType.Bit);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.isSealed;
//                command.Parameters.Add(parameter);
//                SqlDataReader reader = command.ExecuteReader();
//                if (reader.Read())
//                {
//                    e.version = (byte[])reader[0]; ok = true;
//                }
//                reader.Close(); connection.Close();
//            }
//            if (!ok) throw new ApplicationException("Error executing insert command.");
//        }
//        void IDataMapper.Update(IPersistent entity)
//        {
//            Entity e = (Entity)entity;
//            bool ok = false; int rows_affected = 0;
//            using (SqlConnection connection = new SqlConnection(ConnectionString))
//            {
//                connection.Open();
//                SqlCommand command = connection.CreateCommand();
//                command.CommandType = CommandType.Text;
//                command.CommandText = UpdateCommandText;
//                SqlParameter parameter = null;
//                parameter = new SqlParameter("key", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("version", SqlDbType.Timestamp);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.version;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("namespace", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e._namespace == null) ? Guid.Empty : e._namespace.Identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("owner", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.owner == null) ? Guid.Empty : e.owner.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("parent", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.parent == null) ? Guid.Empty : e.parent.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("name", SqlDbType.NVarChar);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.name == null) ? string.Empty : e.name;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("code", SqlDbType.Int);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.code;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("alias", SqlDbType.NVarChar);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = (e.alias == null) ? string.Empty : e.alias;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("is_abstract", SqlDbType.Bit);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.isAbstract;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("is_sealed", SqlDbType.Bit);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.isSealed;
//                command.Parameters.Add(parameter);
//                using (SqlDataReader reader = command.ExecuteReader())
//                {
//                    if (reader.Read())
//                    {
//                        rows_affected = reader.GetInt32(0);
//                        e.version = (byte[])reader[1];
//                        if (rows_affected == 0)
//                        {
//                            e.state = PersistentState.Changed;
//                        }
//                        else
//                        {
//                            ok = true;
//                        }
//                    }
//                    else
//                    {
//                        e.state = PersistentState.Deleted;
//                    }
//                }
//            }
//            if (!ok) throw new OptimisticConcurrencyException(e.state.ToString());
//        }
//        void IDataMapper.Delete(IPersistent entity)
//        {
//            Entity e = (Entity)entity;
//            bool ok = false;
//            using (SqlConnection connection = new SqlConnection(ConnectionString))
//            {
//                connection.Open();
//                SqlCommand command  = connection.CreateCommand();
//                command.CommandType = CommandType.Text;
//                command.CommandText = DeleteCommandText;
//                SqlParameter parameter = null;
//                parameter = new SqlParameter("key", SqlDbType.UniqueIdentifier);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.identity;
//                command.Parameters.Add(parameter);
//                parameter = new SqlParameter("version", SqlDbType.Timestamp);
//                parameter.Direction = ParameterDirection.Input;
//                parameter.Value = e.version;
//                command.Parameters.Add(parameter);
//                SqlDataReader reader = command.ExecuteReader();
//                if (reader.Read()) { ok = (int)reader[0] > 0; }
//                reader.Close(); connection.Close();
//            }
//            if (!ok) throw new ApplicationException("Error executing delete command.");
//        }
//        public static Entity Select(Guid identity)
//        {
//            Entity entity = null;
//            IPersistentContext context = MetadataPersistentContext.Current;
//            IReferenceObjectFactory factory = MetadataPersistentContext.Current.Factory;
//            using (SqlConnection connection = new SqlConnection(context.ConnectionString))
//            using (SqlCommand command = connection.CreateCommand())
//            {
//                connection.Open();
//                command.CommandType = CommandType.Text;
//                command.CommandText = SelectCommandText;
//                SqlParameter parameter = new SqlParameter("key", SqlDbType.UniqueIdentifier)
//                {
//                    Direction = ParameterDirection.Input,
//                    Value = identity
//                };
//                command.Parameters.Add(parameter);
//                using (SqlDataReader reader = command.ExecuteReader())
//                {
//                    if (reader.Read())
//                    {
//                        entity = (Entity)context.Factory.New(typeof(Entity), identity);
//                        // check if nothing was found in IdentityMap - in-memory cash
//                        if (entity.State == PersistentState.New)
//                        {
//                            entity.State = PersistentState.Loading;
//                            Guid guid;
//                            entity._namespace = factory.New<Namespace>((Guid)reader[0]);
//                            guid = (Guid)reader[1];
//                            entity.owner = (guid == Guid.Empty) ? null : factory.New<Entity>(guid);
//                            guid = (Guid)reader[2];
//                            entity.parent = (guid == Guid.Empty) ? null : factory.New<Entity>(guid);
//                            entity.name = (string)reader[3];
//                            entity.code = (int)reader[4];
//                            entity.version = (byte[])reader[5];
//                            entity.alias = reader.GetString(6);
//                            entity.isAbstract = reader.GetBoolean(7);
//                            entity.isSealed = reader.GetBoolean(8);
//                            entity.State = PersistentState.Original;
//                        }
//                    }
//                }
//            }
//            return entity;
//        }
//    }
//}