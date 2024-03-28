using DaJet.Data;
using System;
using System.Collections;

namespace DaJet.Sqlite
{
    public sealed class NamespaceDataMapper : IDataMapper
    {
        private readonly int MY_TYPE_CODE;
        private readonly IDataSource _source;
        #region "SQL"

        #endregion
        public NamespaceDataMapper(IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(NamespaceRecord));
        }
        public IEnumerable Select()
        {
            throw new NotImplementedException();
        }
        public IEnumerable Select(Entity owner)
        {
            throw new NotImplementedException();
        }
        public EntityObject Select(string name)
        {
            throw new NotImplementedException();
        }
        public EntityObject Select(Guid idenity)
        {
            throw new NotImplementedException();
        }
        public void Insert(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public void Update(EntityObject entity)
        {
            throw new NotImplementedException();
        }
        public void Delete(Entity entity)
        {
            throw new NotImplementedException();
        }
    }
}