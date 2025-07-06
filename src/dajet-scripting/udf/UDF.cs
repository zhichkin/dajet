namespace DaJet.Scripting
{
    public static class UDF
    {
        private static Dictionary<string, IUserDefinedFunction> _functions = new();
        static UDF()
        {
            Register(UDF_JSON.Name, new UDF_JSON());
            Register(UDF_CAST.Name, new UDF_CAST());
            Register(UDF_TYPEOF.Name, new UDF_TYPEOF());
            Register(UDF_UUIDOF.Name, new UDF_UUIDOF());
            Register(UDF_UUID1C.Name, new UDF_UUID1C());
        }
        public static bool TryGet(in string name, out IUserDefinedFunction function)
        {
            return _functions.TryGetValue(name, out function);
        }
        public static void Register(in string name, in IUserDefinedFunction function)
        {
            ArgumentNullException.ThrowIfNull(function, nameof(function));
            ArgumentNullException.ThrowIfNullOrWhiteSpace(name, nameof(name));
            
            _ = _functions.TryAdd(name, function);
        }
    }
}