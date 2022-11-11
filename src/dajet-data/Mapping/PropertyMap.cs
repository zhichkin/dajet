using System.Data;

namespace DaJet.Data.Mapping
{
    public sealed class PropertyMap
    {
        public PropertyMap() { }
        public int YearOffset { get; set; } = 0;
        public Type Type { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public int TypeCode { get; set; } = 0;
        public Dictionary<ColumnPurpose, ColumnMap> Columns { get; } = new();
        public void ToColumn(ColumnMap column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            Columns.Add(column.Purpose, column);
        }
        public void ToColumns(List<ColumnMap> columns)
        {
            foreach (ColumnMap column in columns)
            {
                ToColumn(column);
            }
        }

        #region "GET VALUE FROM DATA READER"

        public object? GetValue(in IDataReader reader)
        {
            if (Columns.Count == 0)
            {
                return null;
            }
            else if (Columns.Count == 1)
            {
                return GetSingleValue(in reader);
            }
            else
            {
                return GetMultipleValue(in reader);
            }
        }
        private int GetOrdinal(in IDataReader reader, ColumnPurpose purpose, out ColumnMap column)
        {
            if (Columns.Count == 1)
            {
                purpose = ColumnPurpose.Default;
            }

            if (!Columns.TryGetValue(purpose, out column!) || column == null)
            {
                return -1;
            }

            return reader.GetOrdinal(string.IsNullOrEmpty(column.Alias) ? column.Name : column.Alias);
        }
        private object? GetSingleValue(in IDataReader reader)
        {
            if (Type == typeof(Guid)) { return GetUuid(in reader); }
            else if (Type == typeof(bool)) { return GetBoolean(in reader); }
            else if (Type == typeof(decimal)) { return GetNumeric(in reader); }
            else if (Type == typeof(DateTime)) { return GetDateTime(in reader); }
            else if (Type == typeof(string)) { return GetString(in reader); }
            else if (Type == typeof(byte[])) { return GetBinary(in reader); }
            else if (Type == typeof(EntityRef)) { return GetEntityRef(in reader); }

            throw new NotSupportedException($"Unsupported data type: {Type}");
        }
        private object? GetMultipleValue(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.Tag, out _);

            if (ordinal == -1)
            {
                // Union value without _TYPE discriminator field
                return GetEntityRef(in reader);
            }

            if (reader.IsDBNull(ordinal))
            {
                return Union.Empty;
            }

            byte tag = ((byte[])reader.GetValue(ordinal))[0]; // _TYPE binary(1)

            object? value;

            if (tag == 1) // Неопределено
            {
                return Union.Empty;
            }
            else if (tag == 2) // Булево
            {
                value = GetBoolean(in reader);
                return (value == null ? Union.Empty : new Union.CaseBoolean((bool)value));
            }
            else if (tag == 3) // Число
            {
                value = GetNumeric(in reader);
                return (value == null ? Union.Empty : new Union.CaseNumeric((decimal)value));
            }
            else if (tag == 4) // Дата
            {
                value = GetDateTime(in reader);
                return (value == null ? Union.Empty : new Union.CaseDateTime((DateTime)value));
            }
            else if (tag == 5) // Строка
            {
                value = GetString(in reader);
                return (value == null ? Union.Empty : new Union.CaseString((string)value));
            }
            else if (tag == 8) // Ссылка
            {
                value = GetEntityRef(in reader);
                return (value == null ? Union.Empty : new Union.CaseEntity((EntityRef)value));
            }

            throw new InvalidOperationException($"Invalid union tag value of {tag}");
        }
        private object? GetUuid(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.Default, out _); // single value type only

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return new Guid(SQLHelper.Get1CUuid((byte[])reader.GetValue(ordinal)));
        }
        private object? GetBoolean(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.Boolean, out ColumnMap column);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            bool value;

            if (reader.GetFieldType(ordinal) == typeof(bool))
            {
                value = reader.GetBoolean(ordinal); // PostgreSql
            }
            else
            {
                value = (((byte[])reader.GetValue(ordinal))[0] == 1); // SqlServer
            }

            if (column.Name == "_Folder" || column.Name == "_folder")
            {
                return !value; // invert - exceptional 1C case
            }
            else
            {
                return value; 
            }
        }
        private object? GetNumeric(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.Numeric, out ColumnMap column);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            if (column.Name == "_KeyField") // binary(4)
            {
                return Convert.ToDecimal(DbUtilities.GetInt32((byte[])reader.GetValue(ordinal)));
            }
            else
            {
                return reader.GetDecimal(ordinal);
            }
        }
        private object? GetDateTime(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.DateTime, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetDateTime(ordinal).AddYears(-YearOffset);
        }
        private object? GetString(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.String, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetString(ordinal);
        }
        private object? GetBinary(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.Default, out _); // single value type only

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return ((byte[])reader.GetValue(ordinal));
        }
        private object? GetEntityRef(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, ColumnPurpose.Identity, out _);

            if (ordinal == -1)
            {
                throw new InvalidOperationException("Entity column mapping is not found");
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            Guid identity = new(SQLHelper.Get1CUuid((byte[])reader.GetValue(ordinal))); // binary(16)

            if (Columns.Count == 1) // single reference type value - RRef
            {
                return new EntityRef(TypeCode, identity);
            }

            ordinal = GetOrdinal(in reader, ColumnPurpose.TypeCode, out _);

            if (ordinal == -1) // union having single reference type
            {
                return new EntityRef(TypeCode, identity);
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            int typeCode = DbUtilities.GetInt32((byte[])reader.GetValue(ordinal)); // binary(4)

            return new EntityRef(typeCode, identity);
        }

        #endregion
    }
}

// Исключения из правил:
// - _KeyField (табличная часть) binary(4) -> int CanBeNumeric
// - _Folder (иерархические ссылочные типы) binary(1) -> bool инвертировать !!!
// - _Version (ссылочные типы) timestamp binary(8) -> IsBinary
// - _Type (тип значений характеристики) varbinary(max) -> IsBinary nullable
// - _RecordKind (вид движения накопления) numeric(1) CanBeNumeric Приход = 0, Расход = 1
// - _DimHash numeric(10) ?!