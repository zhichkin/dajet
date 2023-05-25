using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.Data;

namespace DaJet.Data
{
    public sealed class MsDbConfigurator : IDbConfigurator
    {
        private const string TYPE_EXISTS_COMMAND = "SELECT 1 FROM sys.types WHERE name = '{0}';";
        private const string SELECT_TYPE_COLUMNS =
            "SELECT c.name AS [Name], " +
            "c.column_id   AS [Ordinal]," +
            "d.name        AS [TypeName], " +
            "c.max_length  AS [MaxLength]," +
            "c.precision   AS [Precision]," +
            "c.scale       AS [Scale]," +
            "c.is_nullable AS [IsNullable] " +
            "FROM sys.table_types AS t " +
            "INNER JOIN sys.columns AS c ON t.type_table_object_id = c.object_id " +
            "INNER JOIN sys.types AS d ON d.system_type_id = d.user_type_id AND d.system_type_id = c.system_type_id " +
            "WHERE t.name = '{0}' " +
            "ORDER BY c.column_id ASC;";

        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;
        }
        private List<TypeColumnInfo> SelectTypeColumns(in string identifier)
        {
            List<TypeColumnInfo> columns = new();

            string sql = string.Format(SELECT_TYPE_COLUMNS, identifier);

            foreach (IDataReader reader in _executor.ExecuteReader(sql, 10))
            {
                columns.Add(new TypeColumnInfo()
                {
                    Name = reader.GetString(0),
                    Ordinal = reader.GetInt32(1),
                    Type = reader.GetString(2),
                    MaxLength = reader.GetInt16(3),
                    Precision = reader.GetByte(4),
                    Scale = reader.GetByte(5),
                    IsNullable = reader.GetBoolean(6)
                });
            }

            return columns;
        }
        public bool TryCreateType(in TypeDefinition type)
        {
            throw new NotImplementedException();
        }
        private string[] GetIdentifiers(string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string[] identifiers = metadataName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (identifiers.Length < 2)
            {
                throw new FormatException(nameof(metadataName));
            }

            return identifiers;
        }
        public TypeDefinition GetTypeDefinition(in string identifier)
        {
            string[] identifiers = GetIdentifiers(identifier);

            string sql = string.Format(TYPE_EXISTS_COMMAND, identifier);
            
            if (_executor.ExecuteScalar<int>(in sql, 10) != 1) { return null; }

            List<TypeColumnInfo> columns = SelectTypeColumns(in identifier);

            if (columns.Count == 0) { return null; }

            TypeDefinition type = new() { Name = identifier };

            StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            MetadataProperty property;

            foreach (TypeColumnInfo info in columns)
            {
                string[] qualifiers = info.Name.Split('_', splitOptions);

                int typeCode = 0;
                string name = qualifiers[0];
                char purpose = qualifiers[1][0];

                if (qualifiers.Length == 3)
                {
                    typeCode = int.Parse(qualifiers[2]);
                }

                property = null;

                for (int i = 0; i < type.Properties.Count; i++)
                {
                    if (type.Properties[i].Name == name)
                    {
                        property = type.Properties[i]; break;
                    }
                }

                if (property is null)
                {
                    property = new MetadataProperty()
                    {
                        Name = name
                    };
                    type.Properties.Add(property);
                }

                MetadataColumn column = new()
                {
                    Name = info.Name,
                    TypeName = info.Type,
                    Length = info.MaxLength,
                    Precision = info.Precision,
                    Scale = info.Scale,
                    IsNullable = info.IsNullable
                };

                if (info.Type == "nvarchar" && info.MaxLength > 0)
                {
                    column.Length /= 2;
                }

                property.Columns.Add(column);

                if (purpose == 'L') { property.PropertyType.CanBeBoolean = true; }
                else if (purpose == 'N') { property.PropertyType.CanBeNumeric = true; }
                else if (purpose == 'T') { property.PropertyType.CanBeDateTime = true; }
                else if (purpose == 'S') { property.PropertyType.CanBeString = true; }
                else if (purpose == 'B') { property.PropertyType.IsValueStorage = true; }
                else if (purpose == 'U') { property.PropertyType.IsUuid = true; }
                else if (purpose == 'R')
                {
                    if (typeCode == 0)
                    {
                        column.Purpose = ColumnPurpose.Identity;
                    }
                    property.PropertyType.TypeCode = typeCode;
                    property.PropertyType.Reference = Guid.Empty; //TODO(?): GetMetadataItem(int typeCode)
                    property.PropertyType.CanBeReference = true;
                }
                else if (purpose == 'C')
                {
                    column.Purpose = ColumnPurpose.TypeCode;
                    property.PropertyType.TypeCode = 0;
                    property.PropertyType.Reference = Guid.Empty;
                    property.PropertyType.CanBeReference = true;
                }
            }

            return type;
        }
    }
}