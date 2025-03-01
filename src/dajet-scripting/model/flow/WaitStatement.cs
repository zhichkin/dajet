namespace DaJet.Scripting.Model
{
    public enum WaitKind { All, Any }
    public sealed class WaitStatement : SyntaxNode
    {
        public WaitStatement() { Token = TokenType.WAIT; }
        public WaitKind Kind { get; set; } = WaitKind.All;
        public VariableReference Tasks { get; set; } // array of tasks to wait for completion
        public VariableReference Result { get; set; } // ANY completed task object | ALL tasks completed within timeout
        public int Timeout { get; set; } = 0; // seconds to wait
        public override string ToString()
        {
            return $"WAIT {Kind} {Tasks?.Identifier}";
        }
    }
}