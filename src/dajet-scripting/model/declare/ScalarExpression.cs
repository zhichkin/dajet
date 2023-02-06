namespace DaJet.Scripting.Model
{
    public sealed class ScalarExpression : SyntaxNode
    {
        public string Literal { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"{Token}: {Literal}";
        }
    }
}