using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using System.Data.Common;

namespace DaJet.Data
{
    public interface IDbConnectionFactory
    {
        int GetYearOffset(in Uri uri);
        DbConnection Create(in Uri uri);
        string GetConnectionString(in Uri uri);
        void ConfigureParameters(in DbCommand command, in Dictionary<string, object> parameters, int yearOffset);
    }
    public static class DbConnectionFactory
    {
        public static DbConnection Create(in Uri uri)
        {
            return GetFactory(in uri).Create(in uri);
        }
        public static string GetConnectionString(in Uri uri)
        {
            return GetFactory(in uri).GetConnectionString(in uri);
        }
        public static IDbConnectionFactory GetFactory(in Uri uri)
        {
            if (uri.Scheme == "mssql")
            {
                return new MsConnectionFactory();
            }
            else if (uri.Scheme == "pgsql")
            {
                return new PgConnectionFactory();
            }

            throw new InvalidOperationException($"Unsupported database: [{uri.Scheme}]");
        }
    }
}