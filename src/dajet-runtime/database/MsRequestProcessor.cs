namespace DaJet.Runtime
{
    public sealed class MsRequestProcessor : DbRequestProcessor
    {
        public MsRequestProcessor(in ScriptScope scope) : base(in scope) { }
    }
}