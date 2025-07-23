﻿using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using Microsoft.Data.SqlClient;
using System;
using System.Data.Common;

namespace DaJet.Data
{
    public interface IDbConnectionFactory
    {
        int GetYearOffset(in Uri uri);
        DbConnection Create(in Uri uri);
        DbConnection Create(in string connectionString);
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
            else if (uri.Scheme == "sqlite")
            {
                return new SqliteConnectionFactory();
            }

            throw new InvalidOperationException($"Unsupported database: [{uri.Scheme}]");
        }
        public static IDbConnectionFactory GetFactory(DatabaseProvider provider)
        {
            if (provider == DatabaseProvider.SqlServer)
            {
                return new MsConnectionFactory();
            }
            else if (provider == DatabaseProvider.PostgreSql)
            {
                return new PgConnectionFactory();
            }
            else if (provider == DatabaseProvider.Sqlite)
            {
                return new SqliteConnectionFactory();
            }

            throw new InvalidOperationException($"Unsupported provider: [{provider}]");
        }
        public static string GetCacheKey(DatabaseProvider provider, in string connectionString)
        {
            if (provider == DatabaseProvider.SqlServer)
            {
                SqlConnectionStringBuilder builder = new(connectionString);
                return builder.DataSource + builder.InitialCatalog;
            }

            throw new InvalidOperationException($"Unsupported database provider: [{provider}]");
        }
        public static DatabaseProvider GetDatabaseProvider(in Uri uri)
        {
            if (uri.Scheme == "mssql")
            {
                return DatabaseProvider.SqlServer;
            }
            else if (uri.Scheme == "pgsql")
            {
                return DatabaseProvider.PostgreSql;
            }
            else if (uri.Scheme == "sqlite")
            {
                return DatabaseProvider.Sqlite;
            }

            throw new InvalidOperationException($"Unsupported database provider: [{uri.Scheme}]");
        }
    }
}