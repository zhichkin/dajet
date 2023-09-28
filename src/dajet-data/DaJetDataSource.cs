using DaJet.Model;
using System.Collections;

namespace DaJet.Data
{
    public sealed class DaJetDataSource : IDataSource
    {
        private readonly IDomainModel _domain;
        private readonly DataSourceOptions _options;
        public DaJetDataSource(DataSourceOptions options, IDomainModel domain)
        {
            _domain = domain;
            _options = options;
        }
        public void Create(EntityObject entity)
        {
            if (entity is TreeNodeRecord record)
            {
                new TreeNodeDataMapper(_domain, _options.ConnectionString).Insert(record);
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is TreeNodeRecord record)
            {
                new TreeNodeDataMapper(_domain, _options.ConnectionString).Update(record);
            }
        }
        public void Delete(Entity entity)
        {
            Type type = _domain.GetEntityType(entity.TypeCode);

            if (type == typeof(TreeNodeRecord))
            {
                new TreeNodeDataMapper(_domain, _options.ConnectionString).Delete(entity);
            }
        }
        public IEnumerable Select()
        {
            int typeCode = _domain.GetTypeCode(typeof(TreeNodeRecord));

            if (typeCode == 0)
            {
                throw new InvalidOperationException($"Type [{nameof(TreeNodeRecord)}] not found");
            }

            return Select(typeCode, "parent", Entity.Undefined);
        }
        public EntityObject Select(Entity entity)
        {
            if (entity.IsUndefined || entity.IsEmpty)
            {
                return null;
            }

            Type type = _domain.GetEntityType(entity.TypeCode);

            if (type == typeof(TreeNodeRecord))
            {
                return new TreeNodeDataMapper(_domain, _options.ConnectionString).Select(entity);
            }

            return null;
        }
        public IEnumerable Select(int typeCode, string propertyName, Entity value)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (type == typeof(TreeNodeRecord))
            {
                return new TreeNodeDataMapper(_domain, _options.ConnectionString).Select(propertyName, value);
            }

            return null;
        }
    }
}