using DaJet.Data;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Model;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace DaJet.Metadata.SqlServer
{
    public sealed class MsMetadataProvider : IMetadataProvider
    {
        private string _connectionString;
        public void Configure(in Dictionary<string, string> options)
        {
            if (!options.TryGetValue(nameof(InfoBaseOptions.ConnectionString), out string connectionString))
            {
                throw new InvalidOperationException();
            }

            SqlConnectionStringBuilder builder = new(connectionString);

            _connectionString = builder.ToString();
        }

        public int YearOffset { get { return 0; } }
        public DatabaseProvider DatabaseProvider { get { return DatabaseProvider.SqlServer; } }
        public IQueryExecutor CreateQueryExecutor() { return new MsQueryExecutor(_connectionString); }
        public bool IsRegularDatabase
        {
            get
            {
                IQueryExecutor executor = CreateQueryExecutor();
                string script = SQLHelper.GetTableExistsScript("_yearoffset");
                return !(executor.ExecuteScalar<int>(in script, 10) == 1);
            }
        }
        public IEnumerable<MetadataItem> GetMetadataItems(Guid type)
        {
            IQueryExecutor executor = QueryExecutor.Create(DatabaseProvider.SqlServer, _connectionString);

            string script = SQLHelper.GetTableSelectScript();

            List<MetadataItem> list = new();

            foreach (IDataReader reader in executor.ExecuteReader(script, 10))
            {
                list.Add(new MetadataItem(Guid.Empty, Guid.Empty, reader.GetString(0)));
            }

            return list;
        }
        public bool TryGetEnumValue(in string identifier, out EnumValue value)
        {
            throw new NotImplementedException();
        }
        public bool TryGetExtendedInfo(Guid uuid, out MetadataItemEx info)
        {
            info = MetadataItemEx.Empty; return false;
        }
        public MetadataObject GetMetadataObject(string metadataName)
        {
            Catalog table = new()
            {
                Name = metadataName,
                TableName = metadataName, 
            };

            foreach (SqlFieldInfo column in SQLHelper.GetSqlFields(_connectionString, metadataName))
            {
                MetadataProperty property = new()
                {
                    Name = column.COLUMN_NAME,
                    DbName = column.COLUMN_NAME,
                    PropertyType = InferDataType(column.COLUMN_NAME, column.DATA_TYPE, column.CHARACTER_MAXIMUM_LENGTH)
                };

                property.Columns.Add(new MetadataColumn()
                {
                    Name = column.COLUMN_NAME,
                    TypeName = column.DATA_TYPE,
                    Purpose = ColumnPurpose.Default,
                    IsNullable = column.IS_NULLABLE,
                    Scale = column.NUMERIC_SCALE,
                    Length = column.CHARACTER_MAXIMUM_LENGTH,
                    Precision = column.NUMERIC_PRECISION
                });

                table.Properties.Add(property);
            }

            return table;
        }
        private DataTypeSet InferDataType(string columnName, string typeName, int typeSize)
        {
            DataTypeSet type = new();

            if (typeName == "binary" || typeName == "varbinary") // boolean
            {
                return InferBinaryType(columnName, typeSize);
            }
            else if (typeName == "numeric") // numeric
            {

            }
            else if (typeName == "datetime2") // timestamp without time zone
            {

            }
            else if (typeName == "nchar" || typeName == "nvarchar") // mchar | mvarchar
            {

            }
            else if (typeName == "timestamp") // _Version binary(8) | _version integer
            {

            }


            return type;
        }
        private DataTypeSet InferBinaryType(string columnName, int typeSize)
        {
            DataTypeSet type = new();



            return type;
        }



        public TypeDef GetTypeDefinition(in string identifier)
        {
            if (identifier == "Metadata")
            {
                return GetMetadataType();
            }

            throw new NotImplementedException(); //TODO: IMetadataProvider.GetTypeDefinition(...)
        }
        private TypeDef GetMetadataType()
        {
            int ordinal = TypeDef.Entity.Properties.Count;

            TypeDef metadata = new()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Metadata",
                BaseType = TypeDef.Entity
            };

            metadata.Properties.Add(new PropertyDef()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Name",
                Owner = metadata,
                Ordinal = ++ordinal,
                ColumnName = "name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            metadata.Properties.Add(new PropertyDef()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Code",
                Owner = metadata,
                Ordinal = ++ordinal,
                ColumnName = "code",
                IsDbGenerated = true,
                DataType = new UnionType() { IsInteger = true }
            });

            return metadata;
        }
    }
}

//    if (field.IsNullable)
//    {
//        return null;
//    }
//    else if (field.TypeName == "numeric"
//        || field.TypeName == "decimal"
//        || field.TypeName == "smallmoney"
//        || field.TypeName == "money")
//    {
//        return 0;
//    }
//    else if (field.TypeName == "bit")
//    {
//        return false;
//    }
//    else if (field.TypeName == "tinyint")
//    {
//        return (byte)0;
//    }
//    else if (field.TypeName == "smallint")
//    {
//        return (short)0;
//    }
//    else if (field.TypeName == "int")
//    {
//        return 0;
//    }
//    else if (field.TypeName == "bigint")
//    {
//        return (long)0;
//    }
//    else if (field.TypeName == "float"
//        || field.TypeName == "real")
//    {
//        return 0D;
//    }
//    else if (field.TypeName == "datetime"
//        || field.TypeName == "date"
//        || field.TypeName == "time"
//        || field.TypeName == "datetime2"
//        || field.TypeName == "datetimeoffset")
//    {
//        return new DateTime(1753, 1, 1);
//    }
//    else if (field.TypeName == "smalldatetime")
//    {
//        return new DateTime(1900, 1, 1);
//    }
//    else if (field.TypeName == "char"
//        || field.TypeName == "varchar"
//        || field.TypeName == "nchar"
//        || field.TypeName == "nvarchar"
//        || field.TypeName == "text"
//        || field.TypeName == "ntext")
//    {
//        return string.Empty;
//    }
//    else if (field.TypeName == "binary")
//    {
//        return new byte[field.Length];
//    }
//    else if (field.TypeName == "varbinary"
//        || field.TypeName == "image")
//    {
//        return Guid.Empty.ToByteArray();
//    }
//    else if (field.TypeName == "timestamp"
//        || field.TypeName == "rowversion")
//    {
//        return null; // the value is auto generated by database engine
//    }
//    else if (field.TypeName == "uniqueidentifier")
//    {
//        return Guid.Empty;
//    }
//    else
//    {
//        return null;
//    }