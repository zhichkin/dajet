﻿using System.Collections.Generic;
using System.Data;

namespace DaJet.Data
{
    public interface IQueryExecutor
    {
        string GetDatabaseName();
        T ExecuteScalar<T>(in string script, int timeout);
        void ExecuteNonQuery(in string script, int timeout);
        void TxExecuteNonQuery(in List<string> scripts, int timeout);
        IEnumerable<IDataReader> ExecuteReader(string script, int timeout);
        IEnumerable<IDataReader> ExecuteReader(string script, int timeout, Dictionary<string, object> parameters);
        IEnumerable<IDataReader> TxExecuteReader(string script, int timeout, Dictionary<string, object> parameters);
        IEnumerable<IDataReader> ExecuteBatch(string script, int timeout, Dictionary<string, object> parameters);
    }
}