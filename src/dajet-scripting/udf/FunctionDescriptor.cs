using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class FunctionDescriptor
    {
        public FunctionExpression Node { get; set; }
        public Type ReturnType { get; set; }
        public string Target { get; set; } // variable to set return value to
        
    }
}