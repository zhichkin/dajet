﻿namespace DaJet.Scripting.Model
{
    public sealed class DeleteStatement : SyntaxNode
    {
        public DeleteStatement() { Token = TokenType.DELETE; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; }
        public WhereClause Where { get; set; }
    }
}