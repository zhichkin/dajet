namespace DaJet.Scripting.Model
{
    public sealed class SubqueryExpression : SyntaxNode
    {
        public string Alias { get; set; }
        public SelectStatement QUERY { get; set; }
    }
}