using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Diagnostics;
using System.Text;
using System.Web;

namespace DaJet.Stream
{
    public static class StreamManager
    {
        public static void Process(in string script)
        {
            Stopwatch watch = new();

            watch.Start();

            IProcessor stream = CreateStream(in script);

            watch.Stop();

            long elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline assembled in {elapsed} ms");

            watch.Restart();

            stream.Process();

            watch.Stop();

            elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline executed in {elapsed} ms");
        }
        private static string FormatErrorMessage(in List<string> errors)
        {
            if (errors is null || errors.Count == 0)
            {
                return "Unknown binding error";
            }

            StringBuilder error = new();

            for (int i = 0; i < errors.Count; i++)
            {
                if (i > 0) { error.AppendLine(); }

                error.Append(errors[i]);
            }

            return error.ToString();
        }
        private static IProcessor CreateStream(in string script)
        {
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out string error))
                {
                    Console.WriteLine(error);
                }
            }

            return StreamFactory.Create(in model);
        }
        internal static IMetadataProvider GetDatabaseContext(in Uri uri)
        {
            string[] userpass = uri.UserInfo.Split(':');

            string connectionString = string.Empty;

            if (uri.Scheme == "mssql")
            {
                var ms = new SqlConnectionStringBuilder()
                {
                    Encrypt = false,
                    DataSource = uri.Host,
                    InitialCatalog = uri.AbsolutePath.Remove(0, 1) // slash
                };

                if (userpass is not null && userpass.Length == 2)
                {
                    ms.UserID = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                    ms.Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
                }
                else
                {
                    ms.IntegratedSecurity = true;
                }

                connectionString = ms.ToString();
            }
            else if (uri.Scheme == "pgsql")
            {
                var pg = new NpgsqlConnectionStringBuilder()
                {
                    Host = uri.Host,
                    Port = uri.Port,
                    Database = uri.AbsolutePath.Remove(0, 1)
                };

                if (userpass is not null && userpass.Length == 2)
                {
                    pg.Username = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                    pg.Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
                }
                else
                {
                    pg.IntegratedSecurity = true;
                }

                connectionString = pg.ToString();
            }

            InfoBaseRecord database = new()
            {
                ConnectionString = connectionString
            };

            return MetadataService.CreateOneDbMetadataProvider(in database);
        }
    }
}