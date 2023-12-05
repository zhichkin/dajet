using System.Collections;
using System.Data.Common;

namespace DaJet.Data.Client
{
    public sealed class OneDbParameterCollection : DbParameterCollection
    {
        private readonly DbParameterCollection _collection;
        private readonly Dictionary<string, object> _parameters = new();
        public OneDbParameterCollection(DbCommand command)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _collection = command.Parameters;
        }
        public override void Clear()
        {
            _collection.Clear();
            _parameters.Clear();
        }
        public override IEnumerator GetEnumerator() { return _parameters.GetEnumerator(); }
        public override int Count { get { throw new NotImplementedException(); } }
        public override object SyncRoot { get { return ((ICollection)_collection).SyncRoot; } }
        public void Add(string name, object value)
        {

        }
        public void SetValue(string name, object value)
        {

        }
        public object GetValue(string name)
        {
            return null;
        }
        public override int Add(object value)
        {
            throw new NotImplementedException();
        }
        public override void AddRange(Array values)
        {
            throw new NotImplementedException();
        }
        public override bool Contains(object value)
        {
            throw new NotImplementedException();
        }
        public override bool Contains(string value)
        {
            throw new NotImplementedException();
        }
        public override void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }
        public override int IndexOf(object value)
        {
            throw new NotImplementedException();
        }
        public override int IndexOf(string parameterName)
        {
            throw new NotImplementedException();
        }
        public override void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }
        public override void Remove(object value)
        {
            throw new NotImplementedException();
        }
        public override void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }
        public override void RemoveAt(string parameterName)
        {
            throw new NotImplementedException();
        }
        protected override DbParameter GetParameter(int index)
        {
            throw new NotImplementedException();
        }
        protected override DbParameter GetParameter(string parameterName)
        {
            throw new NotImplementedException();
        }
        protected override void SetParameter(int index, DbParameter value)
        {
            throw new NotImplementedException();
        }
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            throw new NotImplementedException();
        }
    }
}