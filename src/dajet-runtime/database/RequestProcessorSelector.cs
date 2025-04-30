using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    internal static class RequestProcessorSelector
    {
        internal static IProcessor GetProcessor(in RequestStatement request, in ScriptScope scope)
        {
            Uri uri = new(request.Target);

            if (IsRowSql(in uri))
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
        private static bool IsRowSql(in Uri uri)
        {
            if (uri.Query is not null)
            {
                string[] parameters = uri.Query.Split('?', '&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parameters is not null && parameters.Length > 0)
                {
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        string[] parameter = parameters[i].Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        if (parameter is not null && parameter.Length > 0)
                        {
                            if (parameter[0] == "sql")
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}