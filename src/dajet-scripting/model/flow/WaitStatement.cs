namespace DaJet.Scripting.Model
{
    public enum WaitKind { All, Any }
    public sealed class WaitStatement : SyntaxNode
    {
        public WaitStatement() { Token = TokenType.WAIT; }
        public WaitKind Kind { get; set; } = WaitKind.All;
        public VariableReference Task { get; set; } // ANY completed task object
        public VariableReference Tasks { get; set; } // array of tasks to wait for completion
        public override string ToString()
        {
            return $"WAIT {Kind} {Tasks?.Identifier}";
        }
    }
}