namespace DaJet.Scripting
{
    public enum TokenType
    {
        Script, Ignore, Comment, Comma, EndOfStatement,

        SELECT, INTO, DISTINCT, TOP, FROM, WHERE, GROUP, HAVING, ORDER, BY, ASC, DESC,
        AND, OR, AS, NOT,
        JOIN, LEFT, RIGHT, INNER, FULL, CROSS, ON, APPLY,
        UNION, ALL, UNION_ALL,
        DECLARE, WITH, INSERT, VALUES, UPDATE, DELETE, OUTPUT, SET, UPSERT, IGNORE,
        NOLOCK, ROWLOCK, READPAST, READCOMMITTEDLOCK, SERIALIZABLE, UPDLOCK,
        ROW, ROWS, ONLY, OFFSET, FETCH, FIRST, NEXT,
        SUM, MAX, MIN, AVG, COUNT, ROW_NUMBER,
        OVER, PARTITION, RANGE, BETWEEN, UNBOUNDED, PRECEDING, CURRENT, FOLLOWING,
        CASE, WHEN, THEN, ELSE, END,
        SUBSTRING,

        Identifier, Type, Table, Column, Variable, Enumeration, Star,

        IS, NULL, ISNULL, Boolean, Number, DateTime, String, Binary, Uuid, Entity, Version, Integer,

        Equals, NotEquals, Less, Greater, LessOrEquals, GreaterOrEquals,

        OpenRoundBracket, CloseRoundBracket, OpenCurlyBracket, CloseCurlyBracket, OpenSquareBracket, CloseSquareBracket,
        
        Plus, Minus, Divide, Modulo, Multiply,

        CREATE, TABLE, COMPUTED, TEMPORARY, VARIABLE
    }
}