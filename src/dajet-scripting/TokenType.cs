namespace DaJet.Scripting
{
    public enum TokenType
    {
        Script, Ignore, Comment, Comma, EndOfStatement,

        SELECT, FROM, WHERE, AND, OR, AS, NOT,
        JOIN, LEFT, RIGHT, INNER, FULL, CROSS, ON,
        DECLARE,

        Identifier, Table, Column, Variable, TemporaryTable, Star,

        NULL, Uuid, Boolean, Number, DateTime, String, Binary, Entity,

        Equals, NotEquals, Less, Greater, LessOrEquals, GreaterOrEquals,
        
        OpenRoundBracket, CloseRoundBracket, OpenCurlyBracket, CloseCurlyBracket, OpenSquareBracket, CloseSquareBracket,
        
        Plus, Minus, Divide, Modulo, Multiply
    }
}