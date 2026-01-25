using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    internal static class RequestProcessorSelector
    {
        internal static IProcessor GetProcessor(in RequestStatement request, in ScriptScope scope)
        {
            string connectionString = scope.TransformStringTemplate(request.Target);

            if (connectionString.StartsWith("[mssql]"))
            {
                return new MsRequestProcessor(in scope);
            }
            else if (connectionString.StartsWith("[pgsql]"))
            {
                return new PgRequestProcessor(in scope);
            }
            else if (connectionString.StartsWith("[sqlite]"))
            {
                return new PgRequestProcessor(in scope);
            }

            Uri uri = new(connectionString);

            if (DbUriHelper.IsRowSql(in uri))
            {
                if (uri.Scheme == "mssql")
                {
                    return new MsRequestProcessor(in scope);
                }
                else if (uri.Scheme == "pgsql")
                {
                    return new PgRequestProcessor(in scope);
                }
                else if (uri.Scheme == "sqlite")
                {
                    return new SqliteRequestProcessor(in scope);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported database: [{uri.Scheme}]");
                }
            }
            
            return new ProcedureProcessor(in scope);
        }
    }
}