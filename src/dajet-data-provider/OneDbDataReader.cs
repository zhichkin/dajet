using DaJet.Scripting;
using System.Collections;
using System.Data.Common;

namespace DaJet.Data.Provider
{
    public sealed class OneDbDataReader : DbDataReader
    {
        private readonly DbDataReader _reader;
        private readonly GeneratorResult _generator;
        internal OneDbDataReader(in DbDataReader reader, in GeneratorResult generator) : base()
        {
            _reader = reader;
            _generator = generator;
        }
        public new void Close()
        {
            _reader.Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader.Dispose();
            }
        }

        public override object this[int ordinal] { get { return GetValue(ordinal); } }
        public override object this[string name]
        {
            get
            {
                PropertyMap property;

                for (int ordinal = 0; ordinal < _generator.Mapper.Properties.Count; ordinal++)
                {
                    property = _generator.Mapper.Properties[ordinal];

                    if (property.Name == name)
                    {
                        return property.GetValue(_reader)!;
                    }
                }

                throw new IndexOutOfRangeException(name);
            }
        }

        public override int FieldCount { get { return _generator.Mapper.Properties.Count; } }
        public override int Depth { get { return _reader.Depth; } }
        public override bool HasRows { get { return _reader.HasRows; } }
        public override bool IsClosed { get { return _reader.IsClosed; } }
        public override int RecordsAffected { get { return _reader.RecordsAffected; } }

        public override bool Read()
        {
            return _reader.Read();
        }
        public override bool NextResult()
        {
            return _reader.NextResult();
        }
        public override IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override bool IsDBNull(int ordinal)
        {
            return GetValue(ordinal) is null;
        }
        public override string GetName(int ordinal)
        {
            return _generator.Mapper.Properties[ordinal].Name;
        }
        public override object GetValue(int ordinal)
        {
            return _generator.Mapper.Properties[ordinal].GetValue(_reader)!;
        }
        public TEntity Map<TEntity>() where TEntity : class, new()
        {
            return _generator.Mapper.Map<TEntity>(_reader);
        }
        public void Map<TEntity>(in TEntity entity) where TEntity : class, new()
        {
            _generator.Mapper.Map(_reader, in entity);
        }

        #region "FIELD METADATA"

        public override Type GetFieldType(int ordinal)
        {
            return _generator.Mapper.Properties[ordinal].Type;
        }
        public override string GetDataTypeName(int ordinal)
        {
            return _generator.Mapper.Properties[ordinal].Type.Name;
        }
        public override int GetOrdinal(string name)
        {
            PropertyMap property;

            for (int ordinal = 0; ordinal < _generator.Mapper.Properties.Count; ordinal++)
            {
                property = _generator.Mapper.Properties[ordinal];

                if (property.Name == name)
                {
                    return ordinal;
                }
            }

            throw new IndexOutOfRangeException(name);
        }

        #endregion

        #region "VALUE GETTERS"

        public override int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }
        public override bool GetBoolean(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override byte GetByte(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }
        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }
        public override DateTime GetDateTime(int ordinal)
        {
            throw new NotImplementedException();
        }
        public override decimal GetDecimal(int ordinal)
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
        public override Guid GetGuid(int ordinal)
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
        public override string GetString(int ordinal)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}