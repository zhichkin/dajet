﻿namespace DaJet.Scripting.Model
{
    public enum ExecuteKind { Default, Task, Work, Sync }
    public sealed class ExecuteStatement : SyntaxNode
    {
        // EXECUTE 'file://script.djs' [WITH <parameters>] [INTO <variable>]
        public ExecuteStatement() { Token = TokenType.EXECUTE; }
        public ExecuteKind Kind { get; set; } = ExecuteKind.Default;
        public string Uri { get; set; } // script uri template
        public string Default { get; set; } // default script uri
        public SyntaxNode Name { get; set; } // AS clause: task or work name
        public List<ColumnExpression> Parameters { get; set; } = new(); // WITH clause
        public VariableReference Return { get; set; } // INTO clause
        public override string ToString()
        {
            return $"EXECUTE [{Kind}] {Uri}";
        }
    }
}