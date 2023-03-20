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



        public IDbConfigurator GetDbConfigurator()
        {
            return new MsDbConfigurator(CreateQueryExecutor());
        }
        public TypeDef GetTypeDefinition(in string identifier)
        {
            MsDbConfigurator configurator = new(CreateQueryExecutor());

            return configurator.SelectTypeDef(in identifier);
        }
        public MetadataObject GetMetadataObject(string metadataName)
        {
            TypeDef definition = GetTypeDefinition(in metadataName);

            Catalog table = new()
            {
                Name = definition.Name,
                TableName = definition.TableName
            };

            foreach (PropertyDef propdef in definition.Properties)
            {
                MetadataProperty property = new()
                {
                    Name = propdef.Name,
                    DbName = propdef.ColumnName,
                    PropertyType = GetDataType(propdef)
                };

                UnionTag tag = propdef.DataType.GetSingleTagOrUndefined();

                property.Columns.Add(new MetadataColumn()
                {
                    Name = propdef.ColumnName,
                    TypeName = MsDbConfigurator.GetDbTypeName(tag),
                    Purpose = ColumnPurpose.Default,
                    IsNullable = propdef.IsNullable,
                    Scale = propdef.Qualifier2,
                    Length = propdef.Qualifier1,
                    Precision = propdef.Qualifier1
                });

                table.Properties.Add(property);
            }

            return table;
        }
        private DataTypeSet GetDataType(in PropertyDef property)
        {
            UnionType union = property.DataType;

            DataTypeSet type = new();

            if (union.IsUuid)
            {
                type.IsUuid = union.IsUuid; return type;
            }

            if (union.IsVersion)
            {
                type.IsBinary = union.IsVersion; return type;
            }

            if (union.IsInteger)
            {
                type.CanBeNumeric = union.IsInteger;
                type.NumericPrecision = 19;
                type.NumericScale = 0;
                type.NumericKind = NumericKind.CanBeNegative;
                return type;
            }

            if (union.IsBinary)
            {
                if (property.Qualifier1 > 0)
                {
                    type.IsBinary = union.IsBinary;
                }
                else
                {
                    type.IsValueStorage = union.IsBinary;
                }
                return type;
            }

            if (union.IsBoolean)
            {
                type.CanBeBoolean = union.IsBoolean;
                if (!union.IsUnion) { return type; }
            }

            if (union.IsNumeric)
            {
                type.CanBeNumeric = union.IsNumeric;
                type.NumericPrecision = property.Qualifier1;
                type.NumericScale = property.Qualifier2;
                type.NumericKind = NumericKind.CanBeNegative;
                if (!union.IsUnion) { return type; }
            }

            if (union.IsDateTime)
            {
                type.CanBeDateTime = union.IsDateTime;
                type.DateTimePart = DateTimePart.DateTime;
                if (!union.IsUnion) { return type; }
            }

            if (union.IsString)
            {
                type.CanBeString = union.IsString;
                type.StringLength = property.Qualifier1;
                type.StringKind = StringKind.Variable;
                if (!union.IsUnion) { return type; }
            }

            if (union.IsEntity)
            {
                type.CanBeReference = union.IsEntity;
                type.TypeCode = union.TypeCode;
                type.Reference = Guid.Empty; //TODO: get uuid from relations
                if (!union.IsUnion) { return type; }
            }

            return type;
        }
    }
}