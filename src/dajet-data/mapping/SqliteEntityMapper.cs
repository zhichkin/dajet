using Microsoft.Data.Sqlite;
using System.Data;

namespace DaJet.Data
{
    public sealed class SqliteDataMapper : IEntityMapper
    {
        private int _ordinal = -1;
        private readonly List<SqlitePropertyMapper> _properties = new();
        public List<PropertyMapper> Properties { get; } = new();
        public void Add(PropertyMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            Properties.Add(mapper); //TODO: убрать этот костыль !!!
            _ordinal++;
            _properties.Add(new SqlitePropertyMapper(mapper.Name, _ordinal, mapper.DataType));
        }
        public void Map(in IDbCommand command, in Dictionary<string, object> parameters)
        {
            if (command is not SqliteCommand cmd)
            {
                throw new InvalidOperationException();
            }

            foreach (var parameter in parameters)
            {
                string name = parameter.Key;
                object value = parameter.Value;

                if (value is null)
                {
                    cmd.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (value is bool boolean)
                {
                    cmd.Parameters.AddWithValue(name, boolean ? 1 : 0);
                }
                else if (value is int integer)
                {
                    cmd.Parameters.AddWithValue(name, integer);
                }
                else if (value is long int64)
                {
                    cmd.Parameters.AddWithValue(name, int64);
                }
                else if (value is decimal number)
                {
                    cmd.Parameters.AddWithValue(name, number);
                }
                else if (value is DateTime dateTime)
                {
                    cmd.Parameters.AddWithValue(name, dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                else if (value is string text)
                {
                    cmd.Parameters.AddWithValue(name, text);
                }
                else if (value is Guid uuid)
                {
                    cmd.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else // byte[] -> BLOB
                {
                    cmd.Parameters.AddWithValue(name, value);
                }
            }
        }
        public void Map(in IDataReader reader, in DataObject record)
        {
            SqlitePropertyMapper property;

            int count = _properties.Count;

            for (int i = 0; i < count; i++)
            {
                property = _properties[i];

                if (property is not null)
                {
                    record.SetValue(property.Name, property.GetValue(in reader));
                }
            }
        }
    }
    internal sealed class SqlitePropertyMapper
    {
        private byte[] _uuid = new byte[16];
        internal SqlitePropertyMapper(string name, int ordinal, UnionType type)
        {
            Name = name;
            Type = type;
            Ordinal = ordinal;
        }
        internal int Ordinal { get; private set; }
        internal string Name { get; private set; }
        internal UnionType Type { get; private set; }
        internal object GetValue(in IDataReader reader)
        {
            if (reader.IsDBNull(Ordinal)) { return null; }
            else if (Type.IsBoolean) { return GetBoolean(in reader); }
            else if (Type.IsInteger) { return GetInteger(in reader); }
            else if (Type.IsNumeric) { return GetDecimal(in reader); }
            else if (Type.IsDateTime) { return GetDateTime(in reader); }
            else if (Type.IsString) { return GetString(in reader); }
            else if (Type.IsBinary) { return GetBinary(in reader); }
            else if (Type.IsUuid) { return GetUuid(in reader); }

            throw new NotSupportedException($"[{typeof(SqlitePropertyMapper)}] Unsupported: {Type}");
        }
        private object GetBoolean(in IDataReader reader)
        {
            return reader.GetInt64(Ordinal) == 1;
        }
        private object GetInteger(in IDataReader reader)
        {
            return reader.GetInt64(Ordinal);
        }
        private object GetDecimal(in IDataReader reader)
        {
            return reader.GetDecimal(Ordinal);
        }
        private object GetDateTime(in IDataReader reader)
        {
            string value = reader.GetString(Ordinal);

            if (DateTime.TryParse(value, out DateTime dateTime))
            {
                return dateTime;
            }
            else
            {
                return DateTime.MinValue;
            }
        }
        private object GetString(in IDataReader reader)
        {
            return reader.GetString(Ordinal);
        }
        private object GetBinary(in IDataReader reader)
        {
            return ((byte[])reader.GetValue(Ordinal));
        }
        private object GetUuid(in IDataReader reader)
        {
            _ = reader.GetBytes(Ordinal, 0, _uuid, 0, 16);
            
            return new Guid(_uuid);
        }
    }
}