using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Text;
using System.Web;

namespace DaJet.Stream
{
    public sealed class UseProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly IProcessor _stream;
        private readonly IMetadataProvider _database;
        public UseProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not UseStatement statement)
            {
                throw new InvalidOperationException();
            }

            _database = GetDatabaseContext(in statement);

            foreach (var item in _scope.Variables)
            {
                if (item.Value is DeclareStatement declare)
                {
                    //TODO: configure declare variable
                }
            }

            _stream = StreamFactory.Create(_scope.Children);

            var context = new StreamContext(_scope.Variables);

            context.MapUri(statement.Uri.ToString());
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process()
        {
            _stream?.Process();
            
            _next?.Process();
        }
        private static IMetadataProvider GetDatabaseContext(in UseStatement statement)
        {
            string[] userpass = statement.Uri.UserInfo.Split(':');

            string connectionString = string.Empty;

            if (statement.Uri.Scheme == "mssql")
            {
                var ms = new SqlConnectionStringBuilder()
                {
                    Encrypt = false,
                    DataSource = statement.Uri.Host,
                    InitialCatalog = statement.Uri.AbsolutePath.Remove(0, 1) // slash
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
            else if (statement.Uri.Scheme == "pgsql")
            {
                var pg = new NpgsqlConnectionStringBuilder()
                {
                    Host = statement.Uri.Host,
                    Port = statement.Uri.Port,
                    Database = statement.Uri.AbsolutePath.Remove(0, 1)
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