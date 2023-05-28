namespace DaJet.Data
{
    /// <summary>
    /// Список поддерживаемых провайдеров баз данных (Microsoft SQL Server или PostgreSQL)
    /// </summary>
    public enum DatabaseProvider
    {
        ///<summary>Microsoft SQL Server database provider</summary>
        SqlServer,
        ///<summary>PostgreSQL database provider</summary>
        PostgreSql,
        ///<summary>Sqlite database provider</summary>
        Sqlite
    }
}