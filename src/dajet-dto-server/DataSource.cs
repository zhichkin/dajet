using DaJet.Model;
using Microsoft.Data.Sqlite;

namespace DaJet.Dto.Server
{
    public sealed class DataSource : IDataSource
    {
        private const string CREATE_TABLE_COMMAND = "CREATE TABLE IF NOT EXISTS " +
            "maintree (uuid TEXT NOT NULL, parent TEXT NOT NULL, name TEXT NOT NULL, is_folder INTEGER NOT NULL, " +
            "entity_type INTEGER NOT NULL, entity_uuid TEXT NOT NULL, PRIMARY KEY (uuid)) WITHOUT ROWID;";

        private readonly IDomainModel _domain;
        private readonly DataSourceOptions _options;
        public DataSource(DataSourceOptions options, IDomainModel domain)
        {
            _domain = domain;
            _options = options;
            InitializeDatabase();
        }
        private void InitializeDatabase()
        {
            using (SqliteConnection connection = new(_options.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = CREATE_TABLE_COMMAND;

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        private IDataMapper GetDataMapper(IPersistent entity)
        {
            if (entity is TreeNodeRecord)
            {
                return new TreeNodeRecordDataMapper(_domain, _options.ConnectionString);
            }
            
            throw new InvalidOperationException("Data mapper for [TreeNodeRecord] is not found");
        }
        public void Create(IPersistent entity)
        {
            GetDataMapper(entity).Insert(entity);
        }
        public void Select(IPersistent entity)
        {
            GetDataMapper(entity).Select(entity);
        }
        public void Update(IPersistent entity)
        {
            GetDataMapper(entity).Update(entity);
        }
        public void Delete(IPersistent entity)
        {
            GetDataMapper(entity).Delete(entity);
        }
        public List<EntityObject> Select(QueryObject query)
        {
            List<EntityObject> list = new();

            using (SqliteConnection connection = new(_options.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = query.Query;

                    if (query.Parameters is not null)
                    {
                        foreach (var parameter in query.Parameters)
                        {
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
                        }
                    }
                    
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Guid identity = new(reader.GetString(0));
                            TreeNodeRecord record = _domain.New<TreeNodeRecord>(identity);

                            record.Parent = _domain.New<TreeNodeRecord>(new Guid(reader.GetString(1)));
                            record.Name = reader.GetString(2);
                            record.IsFolder = (reader.GetInt64(3) == 1L);

                            long code = reader.GetInt64(4);
                            Guid uuid = new(reader.GetString(5));
                            record.Value = _domain.Create((int)code, uuid);

                            list.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public Task<List<EntityObject>> SelectAsync(QueryObject query)
        {
            return Task.FromResult(Select(query));
        }
    }
}