namespace DaJet.Scripting
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FunctionAttribute : Attribute
    {
        public FunctionAttribute(string name)
        {
            Name = name;
        }
        public string Name { get; private set; }
    }
}