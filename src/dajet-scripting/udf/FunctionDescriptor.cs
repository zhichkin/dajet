namespace DaJet.Scripting
{
    public sealed class FunctionDescriptor
    {
        public string Name { get; set; }
        public Type ReturnType { get; set; }
        public string Target { get; set; } // variable to set return value to
        public List<string> Parameters { get; } = new();
    }
}