using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Text;
using System.Web;

namespace DaJet.Data.SqlServer
{
    internal sealed class MsConnectionFactory : IDbConnectionFactory
    {
        public DbConnection Create(in Uri uri)
        {
            return new SqlConnection(GetConnectionString(in uri));
        }
        public DbConnection Create(in string connectionString)
        {
            return new SqlConnection(connectionString);
        }
        public string GetConnectionString(in Uri uri)
        {
            string server = string.Empty;
            string database = string.Empty;

            if (uri.Segments.Length == 3)
            {
                server = $"{uri.Host}{(uri.Port > 0 ? ":" + uri.Port.ToString() : string.Empty)}\\{uri.Segments[1].TrimEnd('/')}";
                database = uri.Segments[2].TrimEnd('/');
            }
            else
            {
                server = uri.Host + (uri.Port > 0 ? ":" + uri.Port.ToString() : string.Empty);
                database = uri.Segments[1].TrimEnd('/');
            }

            var builder = new SqlConnectionStringBuilder()
            {
                Encrypt = false,
                DataSource = server,
                InitialCatalog = database
            };

            string[] userpass = uri.UserInfo.Split(':');

            if (userpass is not null && userpass.Length == 2)
            {
                builder.UserID = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                builder.Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            return builder.ToString();
        }
        public int GetYearOffset(in Uri uri)
        {
            using (SqlConnection connection = new(GetConnectionString(in uri)))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '_YearOffset';";

                    object value = command.ExecuteScalar();

                    if (value is null)
                    {
                        return -1;
                    }

                    command.CommandText = "SELECT TOP 1 [Offset] FROM [_YearOffset];";

                    value = command.ExecuteScalar();

                    if (value is not int offset)
                    {
                        return 0;
                    }

                    return offset;
                }
            }
        }
        public void ConfigureParameters(in DbCommand command, in Dictionary<string, object> parameters, int yearOffset)
        {
            if (command is not SqlCommand cmd)
            {
                throw new InvalidOperationException($"{nameof(command)} is not type of {typeof(SqlCommand)}");
            }

            cmd.Parameters.Clear();

            foreach (var parameter in parameters)
            {
                string name = parameter.Key.StartsWith('@') ? parameter.Key[1..] : parameter.Key;

                if (parameter.Value is null)
                {
                    cmd.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (parameter.Value is Entity entity)
                {
                    cmd.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else if (parameter.Value is bool boolean)
                {
                    cmd.Parameters.AddWithValue(name, new byte[] { Convert.ToByte(boolean) });
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    cmd.Parameters.AddWithValue(name, dateTime.AddYears(yearOffset));
                }
                else if (parameter.Value is Guid uuid)
                {
                    cmd.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else // int, decimal, string, byte[]
                {
                    cmd.Parameters.AddWithValue(name, parameter.Value);
                }

                //TODO: user-defined type - table-valued parameter
                //else if (parameter.Value is List<DataObject> table)
                //{
                //    DeclareStatement declare = GetDeclareStatementByName(in model, parameter.Key);

                //    parameters[parameter.Key] = new TableValuedParameter()
                //    {
                //        Name = parameter.Key,
                //        Value = table,
                //        DbName = declare is null ? string.Empty : declare.Type.Identifier
                //    };
                //}

                //else if (parameter.Value is List<Dictionary<string, object>> table)
                //{
                //    DeclareStatement declare = GetDeclareStatementByName(in model, parameter.Key);

                //    parameters[parameter.Key] = new TableValuedParameter()
                //    {
                //        Name = parameter.Key,
                //        Value = table,
                //        DbName = declare is null ? string.Empty : declare.Type.Identifier
                //    };
                //}
            }
        }

        public string GetCacheKey(in Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Scheme != "mssql")
            {
                throw new ArgumentOutOfRangeException(nameof(uri), $"Invalid schema name [{uri.Scheme}]");
            }

            string connectioString = GetConnectionString(in uri);
            bool useExtensions = DbUriHelper.UseExtensions(in uri);

            return GetCacheKey(in connectioString, useExtensions);
        }
        public string GetCacheKey(in string connectionString, bool useExtensions)
        {
            try
            {
                SqlConnectionStringBuilder builder = new(connectionString);

                string key = string.Format("mssql:{0}:{1}:{2}",
                    builder.DataSource,
                    builder.InitialCatalog,
                    useExtensions).ToLowerInvariant();

                return key;
            }
            catch
            {
                throw;
            }
        }
    }
}