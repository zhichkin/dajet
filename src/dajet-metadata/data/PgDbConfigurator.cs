using DaJet.Data.PostgreSql;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
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
        private static Dictionary<string, Dictionary<string, Type>> _types = new();

        private const string TYPE_SELECT_COMMAND = "SELECT typname FROM pg_type WHERE typtype = 'c';";
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

        private readonly IMetadataProvider _provider;
        public PgDbConfigurator(IMetadataProvider provider)
        {
            _provider = provider;
        }
        private List<TypeColumnInfo> SelectTypeColumns(in string identifier)
        {
            List<TypeColumnInfo> columns = new();

            string sql = string.Format(SELECT_TYPE_COLUMNS, identifier);

            foreach (IDataReader reader in _provider.CreateQueryExecutor().ExecuteReader(sql, 10))
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
        public bool TryCreateType(in EntityDefinition type)
        {
            throw new NotImplementedException();
        }
        public EntityDefinition GetTypeDefinition(in string identifier)
        {
            string sql = string.Format(TYPE_EXISTS_COMMAND, identifier);

            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            if (executor.ExecuteScalar<int>(in sql, 10) != 1) { return null; }

            List<TypeColumnInfo> columns = SelectTypeColumns(in identifier);

            if (columns.Count == 0) { return null; }

            EntityDefinition type = new() { Name = identifier };

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

        public Type GetUserDefinedType(in string typeName)
        {
            string database = _provider.CreateQueryExecutor().GetDatabaseName();

            if (_types.TryGetValue(database, out Dictionary<string, Type> types))
            {
                if (types.TryGetValue(typeName, out Type type))
                {
                    return type;
                }
            }

            return null;
        }
        public List<EntityDefinition> SelectTypeDefinitions()
        {
            List<string> names = new();
            List<EntityDefinition> types = new();

            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            foreach (IDataReader reader in executor.ExecuteReader(TYPE_SELECT_COMMAND, 10))
            {
                names.Add(reader.GetString(0));
            }

            foreach (string name in names)
            {
                types.Add(GetTypeDefinition(in name));
            }

            return types;
        }
        public void InitializeUserDefinedTypes()
        {
            List<EntityDefinition> definitions = SelectTypeDefinitions();

            if (definitions.Count == 0) { return; }

            IQueryExecutor executor = new PgQueryExecutor(_provider.ConnectionString);

            string database = executor.GetDatabaseName();

            Dictionary<string, Type> types = new();

            foreach (EntityDefinition definition in definitions)
            {
                Type type = BuildUserDefinedType(in definition);
                types.Add(definition.Name, type);
            }

            _types.Add(database, types);
        }
        private Type BuildUserDefinedType(in EntityDefinition definition)
        {
            //AppDomain.CurrentDomain.
            
            AssemblyName name = new("pgasm");
            AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            ModuleBuilder mb = ab.DefineDynamicModule(name.Name);
            TypeBuilder tb = mb.DefineType(definition.Name, TypeAttributes.Public);

            Type[] ctr_params = new Type[] { typeof(string) };
            ConstructorInfo ctr = typeof(PgNameAttribute).GetConstructor(ctr_params);

            foreach (MetadataProperty property in definition.Properties)
            {
                Type propertyType = typeof(byte[]);

                foreach (MetadataColumn column in property.Columns)
                {
                    if (column.TypeName == "boolean") { propertyType = typeof(bool); }
                    else if (column.TypeName.StartsWith("numeric")) { propertyType = typeof(decimal); }
                    else if (column.TypeName.StartsWith("timestamp")) { propertyType = typeof(DateTime); }
                    else if (column.TypeName.StartsWith("mvarchar")) { propertyType = typeof(string); }
                    else if (column.TypeName == "bytea") { propertyType = typeof(byte[]); }

                    PropertyBuilder pb = tb.DefineProperty(property.Name, PropertyAttributes.HasDefault, propertyType, null);
                    
                    pb.SetCustomAttribute(new CustomAttributeBuilder(ctr, new object[] { column.Name }));
                }
            }

            return tb.CreateType();
        }
    }
}