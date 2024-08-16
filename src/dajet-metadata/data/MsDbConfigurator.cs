using DaJet.Metadata;
using DaJet.Metadata.Core;
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

        private const string SELECT_INFORMATION_SCHEMA_TABLES =
            "SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES";
        private const string SELECT_INFORMATION_SCHEMA_COLUMNS =
            "SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, " +
            "CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, " +
            "CASE WHEN IS_NULLABLE = 'NO' THEN CAST(0x00 AS bit) ELSE CAST(0x01 AS bit) END AS IS_NULLABLE " +
            "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TABLE_NAME";

        private readonly IQueryExecutor _executor;
        private readonly IMetadataProvider _provider;
        public MsDbConfigurator(IMetadataProvider provider)
        {
            _provider = provider;
            _executor = _provider.CreateQueryExecutor();
        }
        public bool TryConfigureDatabase(out string error) { throw new NotImplementedException(); }
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
        public bool TryCreateType(in UserDefinedType type)
        {
            throw new NotImplementedException();
        }
        public UserDefinedType GetTypeDefinition(in string identifier)
        {
            string sql = string.Format(TYPE_EXISTS_COMMAND, identifier);
            
            if (_executor.ExecuteScalar<int>(in sql, 10) != 1) { return null; }

            List<TypeColumnInfo> columns = SelectTypeColumns(in identifier);

            if (columns.Count == 0) { return null; }

            UserDefinedType type = new() { Name = identifier };

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
                    property.PropertyType.CanBeReference = true;

                    if (typeCode == 0)
                    {
                        column.Purpose = ColumnPurpose.Identity;
                        property.PropertyType.TypeCode = 0;
                        property.PropertyType.Reference = Guid.Empty;
                    }
                    else
                    {
                        MetadataItem item = _provider.GetMetadataItem(typeCode);
                        property.PropertyType.TypeCode = typeCode;
                        property.PropertyType.Reference = item.Uuid;
                    }
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
        public TableDefinition GetTableDefinition(in string identifier)
        {
            if (identifier == "INFORMATION_SCHEMA.TABLES")
            {
                return GetInformationSchemaTables();
            }
            
            if (identifier == "INFORMATION_SCHEMA.COLUMNS")
            {
                return GetInformationSchemaColumns();
            }

            return GetDatabaseMetadata(in identifier);
        }
        private static TableDefinition GetInformationSchemaTables()
        {
            TableDefinition table = new()
            {
                Name = "INFORMATION_SCHEMA.TABLES",
                TableName = "INFORMATION_SCHEMA.TABLES"
            };

            ConfigureTableCatalogProperty(in table);
            ConfigureTableSchemaProperty(in table);
            ConfigureTableNameProperty(in table);
            ConfigureTableTypeProperty(in table);

            return table;
        }
        private static void ConfigureTableCatalogProperty(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "TABLE_CATALOG",
                DbName = "TABLE_CATALOG",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 128;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_CATALOG",
                Length = 128,
                TypeName = "nvarchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void ConfigureTableSchemaProperty(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "TABLE_SCHEMA",
                DbName = "TABLE_SCHEMA",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 128;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_SCHEMA",
                Length = 128,
                TypeName = "nvarchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void ConfigureTableNameProperty(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "TABLE_NAME",
                DbName = "TABLE_NAME",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 128;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_NAME",
                Length = 128,
                TypeName = "nvarchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void ConfigureTableTypeProperty(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "TABLE_TYPE",
                DbName = "TABLE_TYPE",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 10;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_TYPE",
                Length = 10,
                TypeName = "varchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private TableDefinition GetInformationSchemaColumns()
        {
            TableDefinition table = new()
            {
                Name = "INFORMATION_SCHEMA.COLUMNS",
                TableName = "INFORMATION_SCHEMA.COLUMNS"
            };

            ConfigureTableCatalogProperty(in table); // nvarchar(128)
            ConfigureTableSchemaProperty(in table); // nvarchar(128)
            ConfigureTableNameProperty(in table); // nvarchar(128)
            Configure_ORDINAL_POSITION(in table); // int
            Configure_COLUMN_NAME(in table); // nvarchar(128)
            Configure_DATA_TYPE(in table); // nvarchar(128)
            Configure_CHARACTER_MAXIMUM_LENGTH(in table); // int
            Configure_NUMERIC_PRECISION(in table); // tinyint
            Configure_NUMERIC_SCALE(in table); // int
            Configure_IS_NULLABLE(in table); // varchar(3) { 'YES' | 'NO' }

            return table;
        }
        private static void Configure_ORDINAL_POSITION(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "ORDINAL_POSITION",
                DbName = "ORDINAL_POSITION",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;
            property.PropertyType.NumericScale = 0;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "ORDINAL_POSITION",
                TypeName = "int",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void Configure_COLUMN_NAME(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "COLUMN_NAME",
                DbName = "COLUMN_NAME",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 128;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "COLUMN_NAME",
                Length = 128,
                TypeName = "nvarchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void Configure_DATA_TYPE(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "DATA_TYPE",
                DbName = "DATA_TYPE",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 128;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "DATA_TYPE",
                Length = 128,
                TypeName = "nvarchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void Configure_CHARACTER_MAXIMUM_LENGTH(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "CHARACTER_MAXIMUM_LENGTH",
                DbName = "CHARACTER_MAXIMUM_LENGTH",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;
            property.PropertyType.NumericScale = 0;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "CHARACTER_MAXIMUM_LENGTH",
                TypeName = "int",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void Configure_NUMERIC_PRECISION(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "NUMERIC_PRECISION",
                DbName = "NUMERIC_PRECISION",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 3;
            property.PropertyType.NumericScale = 0;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "NUMERIC_PRECISION",
                TypeName = "tinyint",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void Configure_NUMERIC_SCALE(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "NUMERIC_SCALE",
                DbName = "NUMERIC_SCALE",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;
            property.PropertyType.NumericScale = 0;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "NUMERIC_SCALE",
                TypeName = "int",
                IsNullable = false
            });

            table.Properties.Add(property);
        }
        private static void Configure_IS_NULLABLE(in TableDefinition table)
        {
            MetadataProperty property = new()
            {
                Name = "IS_NULLABLE",
                DbName = "IS_NULLABLE",
                IsDbGenerated = true,
                Purpose = PropertyPurpose.System
            };

            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = 3;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "IS_NULLABLE",
                Length = 3,
                TypeName = "varchar",
                IsNullable = false
            });

            table.Properties.Add(property);
        }

        private TableDefinition GetDatabaseMetadata(in string identifier)
        {
            throw new NotImplementedException();
        }
    }
}