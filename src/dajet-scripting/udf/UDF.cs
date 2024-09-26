using DaJet.Scripting.PostgreSql;

namespace DaJet.Scripting
{
    public static class UDF
    {
        private static Dictionary<string, IUserDefinedFunction> _functions = new();
        static UDF()
        {
            Register("TYPEOF", new TYPEOF_FunctionTranspiler());
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