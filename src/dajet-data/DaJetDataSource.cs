using DaJet.Model;
using System.Collections;

namespace DaJet.Data
{
    public sealed class DaJetDataSource : IDataSource
    {
        private readonly IDomainModel _domain;
        private readonly DataSourceOptions _options;
        private readonly Dictionary<Type, IDataMapper> _mappers = new();
        public DaJetDataSource(DataSourceOptions options, IDomainModel domain)
        {
            _domain = domain;
            _options = options;

            string connectionString = _options.ConnectionString;

            _mappers.Add(typeof(TreeNodeRecord), new TreeNodeDataMapper(_domain, connectionString));
            _mappers.Add(typeof(PipelineRecord), new PipelineDataMapper(_domain, connectionString));
            _mappers.Add(typeof(ProcessorRecord), new ProcessorDataMapper(_domain, connectionString));
            _mappers.Add(typeof(OptionRecord), new OptionDataMapper(_domain, connectionString));
        }
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
    }
}