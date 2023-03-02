using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
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
            if (!options.TryGetValue("ConnectionString", out string connectionString))
            {
                throw new InvalidOperationException();
            }

            SqlConnectionStringBuilder builder = new(connectionString);

            _connectionString = builder.ToString();

            //Type type = typeof(SqlConnectionStringBuilder);

            //foreach (var option in options)
            //{
            //    if (option.Key == "host") { builder.DataSource = option.Value; }
            //    else if (option.Key == "dbname") { builder.InitialCatalog = option.Value; }
            //    else if (option.Key == "user") { builder.UserID = option.Value; }
            //    else if (option.Key == "pswd") { builder.Password = option.Value; }
            //    else
            //    {
            //        PropertyInfo property = type.GetProperty(option.Key);

            //        if (property is not null)
            //        {
            //            if (property.PropertyType == typeof(int))
            //            {
            //                property.SetValue(builder, int.Parse(option.Value));
            //            }
            //            else if (property.PropertyType == typeof(bool))
            //            {
            //                property.SetValue(builder, bool.Parse(option.Value));
            //            }
            //            else if (property.PropertyType == typeof(string))
            //            {
            //                property.SetValue(builder, option.Value);
            //            }
            //        }
            //    }
            //}
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
                    PropertyType = new DataTypeSet() //TODO !!!
                };

                property.Columns.Add(new MetadataColumn()
                {
                    Name = column.COLUMN_NAME,
                    TypeName = column.DATA_TYPE,
                    Purpose = ColumnPurpose.Default,  // TODO !?
                    IsNullable = column.IS_NULLABLE,
                    Scale = column.NUMERIC_SCALE,
                    Length = column.CHARACTER_MAXIMUM_LENGTH,
                    Precision = column.NUMERIC_PRECISION
                });

                table.Properties.Add(property);
            }

            return table;
        }
    }
}