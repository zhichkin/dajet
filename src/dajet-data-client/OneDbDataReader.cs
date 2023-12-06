using System.Collections;
using System.Data.Common;

namespace DaJet.Data.Client
{
    public sealed class OneDbDataReader : DbDataReader
    {
        private int _current = 0;
        private EntityMapper _mapper;
        private readonly DbDataReader _reader;
        private readonly List<EntityMapper> _mappers;
        internal OneDbDataReader(in DbDataReader reader, in List<EntityMapper> mappers) : base()
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));

            if (_mappers.Count == 0)
            {
                throw new InvalidOperationException("Data reader mappers are not provided.");
            }

            _mapper = _mappers[_current]; //NOTE: current data reader mapper
        }
        public EntityMapper Mapper { get { return _mapper; } }

        #region "DbDataReader IMPLEMENTATION"
        public override bool Read() { return _reader.Read(); }
        public override bool NextResult()
        {
            bool moveNext = _reader.NextResult();

            if (moveNext)
            {
                _mapper = _mappers[++_current];
            }

            return moveNext;
        }
        public override void Close() { _reader.Close(); }
        protected override void Dispose(bool disposing) { if (disposing) { _reader.Dispose(); } }
        public override object this[int ordinal] { get { return GetValue(ordinal); } }
        public override object this[string name] { get { return this[GetOrdinal(name)]; } }
        public override int Depth { get { return _reader.Depth; } }
        public override bool HasRows { get { return _reader.HasRows; } }
        public override bool IsClosed { get { return _reader.IsClosed; } }
        public override int RecordsAffected { get { return _reader.RecordsAffected; } }
        public override int FieldCount { get { return _mapper.Properties.Count; } }
        public override bool IsDBNull(int ordinal) { return GetValue(ordinal) is null; }
        public override string GetName(int ordinal) { return _mapper.Properties[ordinal].Name; }
        public override object GetValue(int ordinal) { return _mapper.Properties[ordinal].GetValue(_reader); }
        public override int GetOrdinal(string name)
        {
            PropertyMapper property;

            for (int ordinal = 0; ordinal < _mapper.Properties.Count; ordinal++)
            {
                property = _mapper.Properties[ordinal];

                if (property.Name == name)
                {
                    return ordinal;
                }
            }

            throw new IndexOutOfRangeException(name);
        }
        #endregion

        #region "FIELD METADATA"
        public override Type GetFieldType(int ordinal) { return _mapper.Properties[ordinal].Type; }
        public override string GetDataTypeName(int ordinal) { return _mapper.Properties[ordinal].Type.Name; }
        #endregion

        #region "TYPED VALUE GETTERS"
        public override bool GetBoolean(int ordinal) { return (bool)GetValue(ordinal); }
        public override decimal GetDecimal(int ordinal) { return (decimal)GetValue(ordinal); }
        public override DateTime GetDateTime(int ordinal) { return (DateTime)GetValue(ordinal); }
        public override string GetString(int ordinal) { return (string)GetValue(ordinal); }
        public byte[] GetBinary(int ordinal) { return (byte[])GetValue(ordinal); }
        public override Guid GetGuid(int ordinal) { return (Guid)GetValue(ordinal); }
        public Entity GetEntity(int ordinal) { return (Entity)GetValue(ordinal); }
        #endregion

        #region "NOT IMPLEMENTED"
        public override IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
        public override byte GetByte(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }
        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }
        
        
        public override double GetDouble(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override float GetFloat(int ordinal)
        {
            throw new NotImplementedException();
        }
        
        public override short GetInt16(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override int GetInt32(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override long GetInt64(int ordinal)
        {
            throw new NotImplementedException();
        }
        
        public override int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }
        #endregion

        public void Map(in DataObject record)
        {
            _mapper.Map(_reader, in record);
        }
        public void Map<T>(in T entity) where T : class
        {
            _mapper.Map(_reader, in entity);
        }
    }
}