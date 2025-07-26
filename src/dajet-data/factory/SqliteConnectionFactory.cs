using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace DaJet.Data.SqlServer
{
    internal sealed class SqliteConnectionFactory : IDbConnectionFactory
    {
        public DbConnection Create(in Uri uri)
        {
            return new SqliteConnection(GetConnectionString(in uri));
        }
        public DbConnection Create(in string connectionString)
        {
            return new SqliteConnection(connectionString);
        }
        private static string GetDatabaseFilePath(in Uri uri)
        {
            if (uri.Scheme != "sqlite")
            {
                throw new InvalidOperationException(uri.ToString());
            }

            string filePath = uri.AbsoluteUri.Replace("sqlite://", string.Empty);

            int question = filePath.IndexOf('?');

            if (question > -1)
            {
                filePath = filePath[..question];
            }

            filePath = filePath.TrimEnd('/').TrimEnd('\\').Replace('/', '\\');

            string databasePath = Path.Combine(AppContext.BaseDirectory, filePath);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                databasePath = databasePath.Replace('\\', '/');
            }

            return databasePath;
        }
        private static SqliteOpenMode GetConnectionMode(in Uri uri)
        {
            if (uri.Query is null)
            {
                return SqliteOpenMode.ReadWriteCreate;
            }

            string mode = string.Empty;

            string[] parameters = uri.Query.Split('?', '&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parameters is not null && parameters.Length > 0)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    string[] parameter = parameters[i].Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (parameter.Length == 2 && parameter[0] == "mode")
                    {
                        mode = parameter[1]; break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(mode))
            {
                return SqliteOpenMode.ReadWriteCreate;
            }

            if (!Enum.TryParse(mode, out SqliteOpenMode value))
            {
                return SqliteOpenMode.ReadWriteCreate;
            }

            return value;
        }
        public string GetConnectionString(in Uri uri)
        {
            var builder = new SqliteConnectionStringBuilder()
            {
                Mode = GetConnectionMode(in uri),
                DataSource = GetDatabaseFilePath(in uri)
            };

            return builder.ToString();
        }
        public int GetYearOffset(in Uri uri)
        {
            using (SqliteConnection connection = new(GetConnectionString(in uri)))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
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
            if (command is not SqliteCommand cmd)
            {
                throw new InvalidOperationException($"{nameof(command)} is not type of {typeof(SqliteCommand)}");
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
            }
        }

        public string GetCacheKey(in Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Scheme != "sqlite")
            {
                throw new ArgumentOutOfRangeException(nameof(uri), $"Invalid schema name [{uri.Scheme}]");
            }

            string connectioString = GetConnectionString(in uri);

            return GetCacheKey(in connectioString, false);
        }
        public string GetCacheKey(in string connectionString, bool useExtensions)
        {
            try
            {
                SqliteConnectionStringBuilder builder = new(connectionString);

                string key = string.Format("sqlite:{0}:{1}",
                    builder.DataSource,
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