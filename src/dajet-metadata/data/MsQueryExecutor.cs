using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace DaJet.Data.SqlServer
{
    public sealed class MsQueryExecutor : QueryExecutor
    {
        public MsQueryExecutor(string connectionString) : base(connectionString) { }
        public override string GetDatabaseName()
        {
            return new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        }
        protected override DbConnection GetDbConnection()
        {
            return new SqlConnection(_connectionString);
        }
        protected override void ConfigureQueryParameters(in DbCommand command, in Dictionary<string, object> parameters)
        {
            if (command is not SqlCommand _command)
            {
                throw new InvalidOperationException(nameof(command));
            }

            foreach (var item in parameters)
            {
                if (item.Value is TableValuedParameter tvp)
                {
                    List<SqlDataRecord> records = CreateTableParameter(in tvp);

                    SqlParameter parameter = _command.Parameters.AddWithValue(tvp.Name, records);
                    parameter.SqlDbType = SqlDbType.Structured;
                    parameter.TypeName = tvp.DbName;
                }
                else
                {
                    _ = _command.Parameters.AddWithValue(item.Key, item.Value);
                }
            }
        }
        private SqlMetaData[] CreateMetaData(in Dictionary<string, object> record)
        {
            SqlMetaData[] metadata = new SqlMetaData[record.Count];

            int index = 0;

            foreach (var item in record) //TODO: build metadata from EntityDefinition !?
            {
                if (item.Value is null) { throw new InvalidOperationException("NULL values is not allowed"); }
                else if (item.Value is bool) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Binary, 1); }
                else if (item.Value is int) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Int); }
                else if (item.Value is decimal) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Decimal, 14, 4); }
                else if (item.Value is DateTime) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.DateTime2); }
                else if (item.Value is string) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.NVarChar, -1); }
                else if (item.Value is byte[]) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.VarBinary, -1); }
                else if (item.Value is Guid) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Binary, 16); }
                else if (item.Value is Entity) { metadata[index] = new SqlMetaData(item.Key, SqlDbType.Binary, 16); }
                else { throw new InvalidOperationException("Unsupported data type"); }

                index++;
            }

            return metadata;
        }
        private List<SqlDataRecord> CreateTableParameter(in TableValuedParameter table)
        {
            List<SqlDataRecord> records = new();

            if (table.Value is null || table.Value.Count == 0)
            {
                return records;
            }

            SqlMetaData[] metadata = CreateMetaData(table.Value[0]);

            foreach (var record in table.Value)
            {
                SqlDataRecord row = new(metadata);

                int index = 0;

                foreach (var column in record)
                {
                    if (column.Value is null) { throw new InvalidOperationException("NULL values is not allowed"); }
                    else if (column.Value is bool boolean) { row.SetValue(index, boolean ? new byte[] { 1 } : new byte[] { 0 }); }
                    else if (column.Value is int integer) { row.SetInt32(index, integer); }
                    else if (column.Value is decimal numeric) { row.SetDecimal(index, numeric); }
                    else if (column.Value is DateTime datetime) { row.SetDateTime(index, datetime); }
                    else if (column.Value is string text) { row.SetString(index, text); }
                    else if (column.Value is byte[] binary) { row.SetValue(index, binary); }
                    else if (column.Value is Guid uuid) { row.SetValue(index, uuid.ToByteArray()); }
                    else if (column.Value is Entity entity) { row.SetValue(index, entity.Identity.ToByteArray()); }
                    else { throw new InvalidOperationException("Unsupported data type"); }

                    index++;
                }

                records.Add(row);
            }

            return records;
        }
    }
}