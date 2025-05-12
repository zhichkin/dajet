namespace DaJet.Runtime
{
    public sealed class PgRequestProcessor : DbRequestProcessor
    {
        public PgRequestProcessor(in ScriptScope scope) : base(in scope) { }
    }
}