namespace DaJet.Scripting
{
    public enum TokenType
    {
        Script, Ignore, Comment, Comma, EndOfStatement,

        SELECT, TOP, FROM, WHERE, GROUP, HAVING, ORDER, BY, ASC, DESC,
        AND, OR, AS, NOT,
        JOIN, LEFT, RIGHT, INNER, FULL, CROSS, ON,
        DECLARE, WITH, CTE, INSERT, UPDATE, DELETE, OUTPUT, SET,
        NOLOCK, ROWLOCK, READPAST, READCOMMITTEDLOCK, SERIALIZABLE, UPDLOCK,
        ROW, ROWS, ONLY, OFFSET, FETCH, FIRST, NEXT,
        SUM, MAX, MIN, AVG, COUNT, DISTINCT,
        OVER, PARTITION, RANGE, BETWEEN, UNBOUNDED, PRECEDING, CURRENT, FOLLOWING,
        CASE, WHEN, THEN, ELSE, END,

        Identifier, Table, Column, Variable, TemporaryTable, Star,

        NULL, Uuid, Boolean, Number, DateTime, String, Binary, Entity,

        Equals, NotEquals, Less, Greater, LessOrEquals, GreaterOrEquals,
        
        OpenRoundBracket, CloseRoundBracket, OpenCurlyBracket, CloseCurlyBracket, OpenSquareBracket, CloseSquareBracket,
        
        Plus, Minus, Divide, Modulo, Multiply
    }
}