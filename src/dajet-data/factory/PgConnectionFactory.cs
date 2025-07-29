using Npgsql;
using System.Data.Common;
using System.Text;
using System.Web;

namespace DaJet.Data.PostgreSql
{
    internal sealed class PgConnectionFactory : IDbConnectionFactory
    {
        //TODO: private static readonly object _cache_lock = new();
        //TODO: private static readonly Dictionary<string, NpgsqlDataSource> _cache = new();
        public DbConnection Create(in Uri uri)
        {
            //TODO:
            //string connectionString = GetConnectionString(in uri);
            //string cacheKey = GetCacheKey(in connectionString, true);
            //NpgsqlDataSource source = GetOrCreateDataSource(in uri);
            //return source.CreateConnection();

            return new NpgsqlConnection(GetConnectionString(in uri));
        }
        public DbConnection Create(in string connectionString)
        {
            return new NpgsqlConnection(connectionString);
        }
        public string GetConnectionString(in Uri uri)
        {
            var builder = new NpgsqlConnectionStringBuilder()
            {
                Host = uri.Host,
                Port = uri.Port,
                Database = uri.Segments[1].TrimEnd('/') //uri.AbsolutePath.Remove(0, 1)
            };

            string[] userpass = uri.UserInfo.Split(':');

            if (userpass is not null && userpass.Length == 2)
            {
                builder.Username = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                builder.Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
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

        public string GetCacheKey(in Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Scheme != "pgsql")
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
                NpgsqlConnectionStringBuilder builder = new(connectionString);

                string key = string.Format("pgsql:{0}:{1}:{2}:{3}",
                    builder.Host,
                    builder.Port == 0 ? 5432 : builder.Port,
                    builder.Database,
                    useExtensions).ToLowerInvariant();

                return key;
            }
            catch
            {
                throw;
            }
        }

        //private NpgsqlDataSource GetOrCreateDataSource(in Uri uri)
        //{
        //    string connectionString = GetConnectionString(in uri);
        //    string cacheKey = GetCacheKey(in connectionString, true);

        //    if (_cache.TryGetValue(cacheKey, out NpgsqlDataSource source))
        //    {
        //        return source; // fast path
        //    }

        //    bool locked = false;

        //    try
        //    {
        //        Monitor.Enter(_cache_lock, ref locked);

        //        if (_cache.TryGetValue(cacheKey, out source))
        //        {
        //            return source; // double-checking
        //        }

        //        source = new NpgsqlDataSourceBuilder(connectionString).Build();

        //        _cache.Add(cacheKey, source);
        //    }
        //    finally
        //    {
        //        if (locked)
        //        {
        //            Monitor.Exit(_cache_lock);
        //        }
        //    }

        //    return source;
        //}
    }
}