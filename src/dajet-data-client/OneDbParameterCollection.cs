using System.Collections;
using System.Data.Common;

namespace DaJet.Data.Client
{
    public sealed class OneDbParameterCollection : DbParameterCollection, IList<OneDbParameter>
    {
        private readonly List<OneDbParameter> _parameters = new();
        public override int Count { get { return _parameters.Count; } }
        public override object SyncRoot { get { return ((ICollection)_parameters).SyncRoot; } }
        public override IEnumerator GetEnumerator() { return _parameters.GetEnumerator(); }
        IEnumerator<OneDbParameter> IEnumerable<OneDbParameter>.GetEnumerator() { return _parameters.GetEnumerator(); }
        public new OneDbParameter this[int index]
        {
            get { return _parameters[index]; }
            set { _parameters[index] = value; }
        }
        public new OneDbParameter this[string parameterName]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    throw new ArgumentNullException(nameof(parameterName));
                }

                OneDbParameter parameter;

                for (int index = 0; index < _parameters.Count; index++)
                {
                    parameter = _parameters[index];

                    if (parameter.ParameterName == parameterName)
                    {
                        return parameter;
                    }
                }

                throw new ArgumentOutOfRangeException(parameterName);
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public override void Clear() { _parameters.Clear(); }
        public void AddWithValue(string parameterName, object value)
        {
            _parameters.Add(new OneDbParameter()
            {
                Value = value,
                ParameterName = parameterName
            });
        }

        public override int Add(object value)
        {
            throw new NotImplementedException();
        }

        public void Add(OneDbParameter item)
        {
            throw new NotImplementedException();
        }
        
        public override void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public bool Remove(OneDbParameter item)
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

        public bool Contains(OneDbParameter item)
        {
            throw new NotImplementedException();
        }

        public override void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(OneDbParameter[] array, int arrayIndex)
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

        public int IndexOf(OneDbParameter item)
        {
            throw new NotImplementedException();
        }

        public override void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, OneDbParameter item)
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