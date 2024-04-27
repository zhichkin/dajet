namespace DaJet.Scripting.Model
{
    public sealed class ApplySequenceStatement : SyntaxNode
    {
        // APPLY SEQUENCE <sequence> ON <table>(<column>) [RECALCULATE]
        public ApplySequenceStatement() { Token = TokenType.SEQUENCE; }
        public string Identifier { get; set; }
        public TableReference Table { get; set; }
        public ColumnReference Column { get; set; }
        public bool ReCalculate { get; set; }
    }
}