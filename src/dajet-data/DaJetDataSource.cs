using DaJet.Model;
using Microsoft.Data.Sqlite;
using System.Collections;
using System.Data;

namespace DaJet.Data
{
    public sealed class DaJetDataSource : IDataSource
    {
        private readonly IDomainModel _domain;
        private readonly DataSourceOptions _options;
        private readonly Dictionary<Type, IDataMapper> _mappers = new();
        public DaJetDataSource(DataSourceOptions options, IDomainModel domain)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            string connectionString = _options.ConnectionString;

            _mappers.Add(typeof(TreeNodeRecord), new TreeNodeDataMapper(_domain, connectionString));
            _mappers.Add(typeof(PipelineRecord), new PipelineDataMapper(_domain, connectionString));
            _mappers.Add(typeof(HandlerRecord), new HandlerDataMapper(_domain, connectionString));
            _mappers.Add(typeof(OptionRecord), new OptionDataMapper(_domain, connectionString));

            ConfigureDatabase();
        }
        private void ConfigureDatabase()
        {
            // create services root nodes

            List<string> services = new() { "flow" };

            IEnumerable<TreeNodeRecord> nodes = Query<TreeNodeRecord>(Entity.Undefined);

            if (nodes is not null)
            {
                foreach (TreeNodeRecord node in nodes)
                {
                    _ = services.Remove(node.Name);
                }
            }

            foreach (string name in services)
            {
                TreeNodeRecord record = _domain.New<TreeNodeRecord>();
                
                record.Name = name;
                record.Value = Entity.Undefined;
                record.Parent = Entity.Undefined;
                record.IsFolder = true;
                
                Create(record);
            }
        }
        public IDomainModel Model { get { return _domain; } }

        public void Create(EntityObject entity)
        {
            if (_mappers.TryGetValue(entity.GetType(), out IDataMapper mapper))
            {
                mapper.Insert(entity);
            }
        }
        public void Update(EntityObject entity)
        {
            if (_mappers.TryGetValue(entity.GetType(), out IDataMapper mapper))
            {
                mapper.Update(entity);
            }
        }
        public void Delete(Entity entity)
        {
            Type type = _domain.GetEntityType(entity.TypeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                mapper.Delete(entity);
            }
        }
        public void Delete<T>(Guid identity) where T : EntityObject
        {
            Entity entity = _domain.GetEntity<T>(identity);

            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                mapper.Delete(entity);
            }
        }

        public EntityObject Select(Entity entity)
        {
            if (entity.IsUndefined || entity.IsEmpty)
            {
                return null;
            }

            Type type = _domain.GetEntityType(entity.TypeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                return mapper.Select(entity.Identity);
            }

            return null;
        }
        public IEnumerable Select(int typeCode)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                return mapper.Select();
            }

            return null;
        }
        public IEnumerable Select(int typeCode, Entity owner)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                return mapper.Select(owner);
            }

            return null;
        }
        public List<IDataRecord> Query(string query, Dictionary<string, object> parameters)
        {
            List<IDataRecord> list = new();

            using (SqliteConnection connection = new(_options.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    foreach(var parameter in parameters)
                    {
                        if (parameter.Value is Guid uuid)
                        {
                            command.Parameters.AddWithValue(parameter.Key, uuid.ToString().ToLower());
                        }
                        else
                        {
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
                        }
                    }

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataRecord record = new();
                            
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                record.SetValue(reader.GetName(i), reader.GetValue(i));
                            }

                            list.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }

        public T Select<T>(Guid identity) where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select(identity) as T;
            }
            return null;
        }
        public T Select<T>(Entity entity) where T : EntityObject
        {
            if (entity.IsUndefined || entity.IsEmpty)
            {
                return null;
            }
            return Select<T>(entity.Identity);
        }
        public IEnumerable<T> Query<T>() where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select() as IEnumerable<T>;
            }
            return null;
        }
        public IEnumerable<T> Query<T>(Entity owner) where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select(owner) as IEnumerable<T>;
            }
            return null;
        }
    }
}