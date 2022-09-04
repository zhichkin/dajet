namespace DaJet.Scripting
{
    public enum TokenType
    {
        Script, Ignore, Comment, Comma, EndOfStatement,

        SELECT, TOP, FROM, WHERE, ORDER, BY, ASC, DESC,
        AND, OR, AS, NOT,
        JOIN, LEFT, RIGHT, INNER, FULL, CROSS, ON,
        DECLARE, WITH, CTE, INSERT, UPDATE, DELETE, OUTPUT, SET,
        NOLOCK, ROWLOCK, READPAST, READCOMMITTEDLOCK, SERIALIZABLE, UPDLOCK,

        Identifier, Table, Column, Variable, TemporaryTable, Star,

        NULL, Uuid, Boolean, Number, DateTime, String, Binary, Entity,

        Equals, NotEquals, Less, Greater, LessOrEquals, GreaterOrEquals,
        
        OpenRoundBracket, CloseRoundBracket, OpenCurlyBracket, CloseCurlyBracket, OpenSquareBracket, CloseSquareBracket,
        
        Plus, Minus, Divide, Modulo, Multiply
    }
}