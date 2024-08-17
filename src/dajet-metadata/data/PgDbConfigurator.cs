using DaJet.Data.PostgreSql;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Data
{
    public sealed class PgDbConfigurator : IDbConfigurator
    {
        private const string TYPE_SELECT_COMMAND = "SELECT typname FROM pg_type WHERE typtype = 'c' AND typname LIKE 'dajet%';";
        private const string TYPE_EXISTS_COMMAND = "SELECT 1 FROM pg_type WHERE typtype = 'c' AND typname = '{0}';";
        private const string SELECT_TYPE_COLUMNS =
            "SELECT a.attname AS \"COLUMN_NAME\", a.attnum AS \"ORDINAL\", " +
            "pg_catalog.format_type(a.atttypid, a.atttypmod) AS \"DATA_TYPE\" " +
            "FROM pg_catalog.pg_attribute a " +
            "WHERE a.attnum > 0 AND NOT a.attisdropped AND a.attrelid = (" +
            "SELECT c.oid FROM pg_catalog.pg_class c " +
            "LEFT JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace " +
            "WHERE c.relkind = 'c' AND c.relname = '{0}' AND pg_catalog.pg_table_is_visible(c.oid))" +
            "ORDER BY a.attnum ASC;";

        private const string SELECT_INFORMATION_SCHEMA_COLUMNS =
            "SELECT ORDINAL_POSITION, COLUMN_NAME, " +
            "CASE WHEN DATA_TYPE = 'USER-DEFINED' THEN UDT_NAME ELSE DATA_TYPE END AS DATA_TYPE, " +
            "CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, " +
            "CASE WHEN IS_NULLABLE = 'NO' THEN FALSE ELSE TRUE END AS IS_NULLABLE " +
            "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TABLE_NAME";

        private static readonly Dictionary<string, Dictionary<string, Type>> _types = new();
        public static Type GetUserDefinedType(in string database, in string typeName)
        {
            if (_types.TryGetValue(database, out Dictionary<string, Type> types))
            {
                if (types.TryGetValue(typeName, out Type type))
                {
                    return type;
                }
            }

            return null;
        }
        public static Dictionary<string, Type> GetUserDefinedTypes(in string database)
        {
            if (_types.TryGetValue(database, out Dictionary<string, Type> types))
            {
                return types;
            }

            return null;
        }
        public static void InitializeUserDefinedTypes(in string connectionString)
        {
            Dictionary<string, List<TypeColumnInfo>> list = SelectTypeDefinitions(in connectionString);

            if (list.Count == 0) { return; }

            string database = new NpgsqlConnectionStringBuilder(connectionString).Database;

            if (_types.ContainsKey(database)) { return; }

            AssemblyName asm = new(database);
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(asm, AssemblyBuilderAccess.Run);
            ModuleBuilder module = assembly.DefineDynamicModule(asm.Name);

            Dictionary<string, Type> types = new();

            foreach (var udt in list)
            {
                Type type = BuildUserDefinedType(in module, udt.Key, udt.Value);

                if (type is not null) { types.Add(udt.Key, type); }
            }

            _types.Add(database, types);
        }
        private static Dictionary<string, List<TypeColumnInfo>> SelectTypeDefinitions(in string connectionString)
        {
            List<string> names = new();
            Dictionary<string, List<TypeColumnInfo>> types = new();

            using (NpgsqlConnection connection = new(connectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = TYPE_SELECT_COMMAND;
                    command.CommandTimeout = 10;

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            names.Add(reader.GetString(0));
                        }
                        reader.Close();
                    }
                }
            }

            foreach (string name in names)
            {
                List<TypeColumnInfo> columns = SelectTypeColumns(in connectionString, in name);

                types.Add(name, columns);
            }

            return types;
        }
        private static List<TypeColumnInfo> SelectTypeColumns(in string connectionString, in string typeName)
        {
            List<TypeColumnInfo> columns = new();

            string sql = string.Format(SELECT_TYPE_COLUMNS, typeName);

            using (NpgsqlConnection connection = new(connectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;
                    command.CommandTimeout = 10;

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add(new TypeColumnInfo()
                            {
                                Name = reader.GetString(0),
                                Ordinal = reader.GetInt32(1),
                                Type = reader.GetString(2)
                            });
                        }
                        reader.Close();
                    }
                }
            }

            return columns;
        }
        private static Type BuildUserDefinedType(in ModuleBuilder module, in string typeName, in List<TypeColumnInfo> columns)
        {
            TypeBuilder tb = module.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public);

            Type[] ctr_params = new Type[] { typeof(string) };
            ConstructorInfo ctr = typeof(PgNameAttribute).GetConstructor(ctr_params);

            Type propertyType = typeof(byte[]);

            StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            foreach (TypeColumnInfo column in columns)
            {
                string[] qualifiers = column.Name.Split('_', splitOptions);

                int typeCode = 0;
                string propertyName = qualifiers[0];
                char purpose = qualifiers[1][0]; // d, l, n, t, s, b, u, c, r

                if (qualifiers.Length == 3) { typeCode = int.Parse(qualifiers[2]); }

                if (column.Type == "boolean") { propertyType = typeof(bool); }
                else if (column.Type.StartsWith("numeric")) { propertyType = typeof(decimal); }
                else if (column.Type.StartsWith("timestamp")) { propertyType = typeof(DateTime); }
                else if (column.Type.StartsWith("text")) { propertyType = typeof(string); }
                else if (column.Type.StartsWith("char")) { propertyType = typeof(string); }
                else if (column.Type.StartsWith("varchar")) { propertyType = typeof(string); }
                else if (column.Type.StartsWith("character")) { propertyType = typeof(string); }
                else if (column.Type.StartsWith("character varying")) { propertyType = typeof(string); }
                else if (column.Type == "bytea") { propertyType = typeof(byte[]); }

                FieldBuilder fb = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
                PropertyBuilder pb = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
                MethodAttributes accessorAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

                // Define the 'get' accessor method.
                MethodBuilder getMethodBuilder = tb.DefineMethod($"get_{propertyName}", accessorAttributes, propertyType, Type.EmptyTypes);
                ILGenerator propertyGetGenerator = getMethodBuilder.GetILGenerator();
                propertyGetGenerator.Emit(OpCodes.Ldarg_0);
                propertyGetGenerator.Emit(OpCodes.Ldfld, fb);
                propertyGetGenerator.Emit(OpCodes.Ret);

                // Define the 'set' accessor method.
                MethodBuilder setMethodBuilder = tb.DefineMethod($"set_{propertyName}", accessorAttributes, null, new Type[] { propertyType });
                ILGenerator propertySetGenerator = setMethodBuilder.GetILGenerator();
                propertySetGenerator.Emit(OpCodes.Ldarg_0);
                propertySetGenerator.Emit(OpCodes.Ldarg_1);
                propertySetGenerator.Emit(OpCodes.Stfld, fb);
                propertySetGenerator.Emit(OpCodes.Ret);

                pb.SetGetMethod(getMethodBuilder);
                pb.SetSetMethod(setMethodBuilder);
                pb.SetCustomAttribute(new CustomAttributeBuilder(ctr, new object[] { column.Name }));
            }

            return tb.CreateType();
        }



        private readonly IMetadataProvider _provider;
        public PgDbConfigurator(IMetadataProvider provider)
        {
            _provider = provider;
        }
        public bool TryConfigureDatabase(out string error) { throw new NotImplementedException(); }
        private List<TypeColumnInfo> SelectTypeColumns(in string identifier)
        {
            List<TypeColumnInfo> columns = new();

            string sql = string.Format(SELECT_TYPE_COLUMNS, identifier);

            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            foreach (IDataReader reader in executor.ExecuteReader(sql, 10))
            {
                columns.Add(new TypeColumnInfo()
                {
                    Name = reader.GetString(0),
                    Ordinal = reader.GetInt32(1),
                    Type = reader.GetString(2)//,
                    //MaxLength = reader.GetInt16(3),
                    //Precision = reader.GetByte(4),
                    //Scale = reader.GetByte(5),
                    //IsNullable = reader.GetBoolean(6)
                });
            }

            return columns;
        }
        public bool TryCreateType(in UserDefinedType type)
        {
            throw new NotImplementedException();
        }
        public Type GetUserDefinedType(in string typeName)
        {
            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            string database = executor.GetDatabaseName();

            if (_types.TryGetValue(database, out Dictionary<string, Type> types))
            {
                if (types.TryGetValue(typeName, out Type type))
                {
                    return type;
                }
            }

            return null;
        }
        public UserDefinedType GetTypeDefinition(in string identifier)
        {
            string sql = string.Format(TYPE_EXISTS_COMMAND, identifier);

            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            if (executor.ExecuteScalar<int>(in sql, 10) != 1) { return null; }

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

                property.Columns.Add(column);

                if (purpose == 'l') { property.PropertyType.CanBeBoolean = true; }
                else if (purpose == 'n') { property.PropertyType.CanBeNumeric = true; }
                else if (purpose == 't') { property.PropertyType.CanBeDateTime = true; }
                else if (purpose == 's') { property.PropertyType.CanBeString = true; }
                else if (purpose == 'b') { property.PropertyType.IsValueStorage = true; }
                else if (purpose == 'u') { property.PropertyType.IsUuid = true; }
                else if (purpose == 'r')
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
                else if (purpose == 'c')
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
            property.PropertyType.StringLength = 64;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_CATALOG",
                Length = 64,
                TypeName = "character varying",
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
            property.PropertyType.StringLength = 64;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_SCHEMA",
                Length = 64,
                TypeName = "character varying",
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
            property.PropertyType.StringLength = 64;
            property.PropertyType.StringKind = StringKind.Variable;

            property.Columns.Add(new MetadataColumn()
            {
                Name = "TABLE_NAME",
                Length = 64,
                TypeName = "character varying",
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
                TypeName = "character varying",
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

            ConfigureTableCatalogProperty(in table);
            ConfigureTableSchemaProperty(in table);
            ConfigureTableNameProperty(in table);
            ConfigureTableCatalogProperty(in table);
            ConfigureTableSchemaProperty(in table);
            ConfigureTableNameProperty(in table);
            Configure_ORDINAL_POSITION(in table);
            Configure_COLUMN_NAME(in table);
            Configure_DATA_TYPE(in table);
            Configure_CHARACTER_MAXIMUM_LENGTH(in table);
            Configure_NUMERIC_PRECISION(in table);
            Configure_NUMERIC_SCALE(in table);
            Configure_IS_NULLABLE(in table);

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
                TypeName = "integer",
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
                Length = 64,
                TypeName = "character varying",
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
                Length = 64,
                TypeName = "character varying",
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
                TypeName = "integer",
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
                TypeName = "integer",
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
                TypeName = "integer",
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
                TypeName = "character varying",
                IsNullable = false
            });

            table.Properties.Add(property);
        }

        private TableDefinition GetDatabaseMetadata(in string identifier)
        {
            TableDefinition table = new()
            {
                Name = identifier,
                TableName = identifier
            };

            Dictionary<string, object> parameters = new()
            {
                { "TABLE_NAME", identifier }
            };

            MetadataProperty property = null;

            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            foreach (IDataReader reader in executor.ExecuteReader(SELECT_INFORMATION_SCHEMA_COLUMNS, 5, parameters))
            {
                int ORDINAL_POSITION = reader.GetInt32(0);
                string COLUMN_NAME = reader.GetString(1);
                string DATA_TYPE = reader.GetString(2);
                int CHARACTER_MAXIMUM_LENGTH = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                int NUMERIC_PRECISION = reader.IsDBNull(4) ? 0 : reader.GetByte(4);
                int NUMERIC_SCALE = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                bool IS_NULLABLE = reader.GetBoolean(6);

                property = new MetadataProperty()
                {
                    Name = COLUMN_NAME,
                    DbName = COLUMN_NAME,
                    PrimaryKey = 0,
                    IsDbGenerated = true
                };

                property.Columns.Add(new MetadataColumn()
                {
                    Name = COLUMN_NAME,
                    TypeName = DATA_TYPE,
                    IsNullable = IS_NULLABLE,
                    Length = CHARACTER_MAXIMUM_LENGTH,
                    Precision = NUMERIC_PRECISION,
                    Scale = NUMERIC_SCALE,
                    IsPrimaryKey = false,
                    KeyOrdinal = 0
                });

                if (DATA_TYPE == "boolean")
                {
                    property.PropertyType.CanBeBoolean = true;
                }
                else if (DATA_TYPE == "numeric")
                {
                    property.PropertyType.CanBeNumeric = true;
                    property.PropertyType.NumericKind = NumericKind.CanBeNegative;
                    property.PropertyType.NumericPrecision = NUMERIC_PRECISION;
                    property.PropertyType.NumericScale = NUMERIC_SCALE;
                }
                else if (DATA_TYPE.StartsWith("timestamp"))
                {
                    property.PropertyType.CanBeDateTime = true;
                    property.PropertyType.DateTimePart = DateTimePart.DateTime;
                }
                else if (DATA_TYPE == "mchar" || DATA_TYPE == "character" || DATA_TYPE == "char")
                {
                    property.PropertyType.CanBeString = true;
                    property.PropertyType.StringKind = StringKind.Fixed;
                    property.PropertyType.StringLength = CHARACTER_MAXIMUM_LENGTH;
                }
                else if (DATA_TYPE == "mvarchar" || DATA_TYPE == "character varying" || DATA_TYPE == "varchar" || DATA_TYPE == "text")
                {
                    property.PropertyType.CanBeString = true;
                    property.PropertyType.StringKind = StringKind.Variable;
                    property.PropertyType.StringLength = CHARACTER_MAXIMUM_LENGTH;
                }
                else if (DATA_TYPE == "bytea")
                {
                    property.PropertyType.IsBinary = true;
                    property.PropertyType.StringLength = CHARACTER_MAXIMUM_LENGTH;
                }
                else if (DATA_TYPE == "uuid")
                {
                    property.PropertyType.IsUuid = true;
                }
                else if (DATA_TYPE == "integer")
                {
                    property.PropertyType.CanBeNumeric = true;
                    property.PropertyType.NumericKind = NumericKind.CanBeNegative;
                    property.PropertyType.NumericPrecision = 10;
                    property.PropertyType.NumericScale = 0;
                }

                table.Properties.Add(property);
            }

            if (table.Properties is null || table.Properties.Count == 0)
            {
                return null; // table not found error
            }

            return table;
        }
    }
}