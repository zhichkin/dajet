using Npgsql;
using System.Data.Common;
using System.Text;
using System.Web;

namespace DaJet.Data.PostgreSql
{
    internal sealed class PgConnectionFactory : IDbConnectionFactory
    {
        public DbConnection Create(in Uri uri)
        {
            return new NpgsqlConnection(GetConnectionString(in uri));
        }
        public string GetConnectionString(in Uri uri)
        {
            var builder = new NpgsqlConnectionStringBuilder()
            {
                Host = uri.Host,
                Port = uri.Port,
                Database = uri.AbsolutePath.Remove(0, 1)
            };

            string[] userpass = uri.UserInfo.Split(':');

            if (userpass is not null && userpass.Length == 2)
            {
                builder.Username = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
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
            using (NpgsqlConnection connection = new(GetConnectionString(in uri)))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '_yearoffset';";

                    object value = command.ExecuteScalar();

                    if (value is null)
                    {
                        return -1;
                    }

                    command.CommandText = "SELECT ofset FROM _yearoffset LIMIT 1;";

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
            if (command is not NpgsqlCommand cmd)
            {
                throw new InvalidOperationException($"{nameof(command)} is not type of {typeof(NpgsqlCommand)}");
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
                else if (parameter.Value is DateTime dateTime)
                {
                    cmd.Parameters.AddWithValue(name, dateTime.AddYears(yearOffset));
                }
                else if (parameter.Value is Guid uuid)
                {
                    cmd.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else // bool, int, decimal, string, byte[]
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
    }
}