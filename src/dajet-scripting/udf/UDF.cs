﻿using DaJet.Scripting.PostgreSql;

namespace DaJet.Scripting
{
    public static class UDF
    {
        private static Dictionary<string, IUserDefinedFunction> _functions = new();
        static UDF()
        {
            Register(UDF_TYPEOF.Name, new UDF_TYPEOF());
            
            Register("ERROR_MESSAGE", new UDF_TYPEOF()); //FIXME: !!! ignore for database statements
        }
        public static void Register(in string name, in IUserDefinedFunction transpiler)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ArgumentNullException.ThrowIfNull(transpiler, nameof(transpiler));
            
            _ = _functions.TryAdd(name, transpiler);
        }
        public static bool TryGet(in string name, out IUserDefinedFunction transpiler)
        {
            return _functions.TryGetValue(name, out transpiler);
        }
    }
}