using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public struct ScriptParser : IDisposable
    {
        private int _current = 0;
        private ScriptToken _token = null;
        private List<ScriptToken> _tokens = null;
        private List<ScriptToken> _ignore = null; // test and debug purposes
        public ScriptParser() { }
        public void Dispose()
        {
            _token = null;
            _tokens?.Clear();
            _tokens = null;
            _current = 0;
            _ignore?.Clear();
            _ignore = null;
        }
        public bool TryParse(in string script, out ScriptModel tree, out string error)
        {
            tree = new();
            error = string.Empty;
            
            using (ScriptTokenizer scanner = new())
            {
                if (!scanner.TryTokenize(in script, out _tokens, out error))
                {
                    return false;
                }
            }
            
            try
            {
                SyntaxNode node;

                while (!EndOfStream())
                {
                    node = statement();

                    if (node != null)
                    {
                        tree.Statements.Add(node);
                    }
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }
            
            Dispose();

            return string.IsNullOrWhiteSpace(error);
        }

        #region "UTILITY FUNCTIONS"
        private bool EndOfStream()
        {
            return (_tokens == null || _current >= _tokens.Count);
        }
        private ScriptToken Current()
        {
            return _tokens[_current];
        }
        private ScriptToken Previous()
        {
            return _tokens[_current - 1];
        }
        private bool Consume()
        {
            if (EndOfStream()) return false;

            _token = _tokens[_current++];

            return (_token != null);
        }
        private bool Check(TokenType type)
        {
            if (EndOfStream()) return false;

            return Current().Type == type;
        }
        private bool Match(params TokenType[] types)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (Check(types[i])) return Consume();
            }
            return false;
        }
        private void Ignore()
        {
            if (Consume())
            {
                if (_ignore == null)
                {
                    _ignore = new List<ScriptToken>();
                }

                _ignore.Add(Previous());
            }
        }
        private void Skip(params TokenType[] types)
        {
            while (!EndOfStream())
            {
                if (Match(types))
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
        }
        #endregion
        
        private SyntaxNode statement()
        {
            if (Match(TokenType.Comment))
            {
                return comment();
            }
            else if (Match(TokenType.DECLARE))
            {
                if (Check(TokenType.Variable))
                {
                    return declare();
                }
                else if (Check(TokenType.Identifier))
                {
                    return statement_with_cte();
                }
            }
            else if (Check(TokenType.SELECT))
            {
                return select_statement();
            }
            else if (Match(TokenType.WITH))
            {
                return statement_with_cte();
            }
            else if (Check(TokenType.DELETE))
            {
                return delete_statement();
            }
            else if (Match(TokenType.EndOfStatement))
            {
                return null!;
            }

            Ignore();

            throw new FormatException($"Unknown statement: {Previous()}");
        }
        private SyntaxNode comment()
        {
            return new CommentStatement()
            {
                Text = Previous().Lexeme
            };
        }
        private SyntaxNode declare()
        {
            DeclareStatement declare = new();

            if (!Match(TokenType.Variable))
            {
                throw new FormatException("Variable identifier expected.");
            }
            else
            {
                declare.Name = Previous().Lexeme;
            }

            if (Match(TokenType.AS))
            {
                // do nothing
            }

            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Variable data type identifier expected.");
            }
            else
            {
                declare.Type = Previous().Lexeme; // TODO: qualify data type !?
            }

            if (Match(TokenType.Equals))
            {
                if (!Match(TokenType.String, TokenType.Boolean, TokenType.Number, TokenType.Entity))
                {
                    throw new FormatException("Scalar expression expected.");
                }
                else
                {
                    declare.Initializer = new ScalarExpression()
                    {
                        Token = Previous().Type,
                        Literal = Previous().Lexeme
                    };
                }
            }

            if (!Match(TokenType.EndOfStatement))
            {
                throw new FormatException("End of DECLARE statement expected.");
            }

            return declare;
        }
        private SyntaxNode statement_with_cte()
        {
            CommonTableExpression root = cte();

            while (Match(TokenType.Comma))
            {
                CommonTableExpression node = cte();

                node.Next = root;

                root = node;
            }

            if (Check(TokenType.SELECT))
            {
                SelectStatement select = select_expression();

                select.CTE = root;

                return select;
            }

            if (Check(TokenType.DELETE))
            {
                DeleteStatement delete = delete_statement();

                delete.CTE = root;

                return delete;
            }

            throw new FormatException("SELECT or DELETE statement expected.");
        }
        private CommonTableExpression cte()
        {
            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Identifier expected.");
            }

            CommonTableExpression cte = new()
            {
                Name = Previous().Lexeme
            };

            Skip(TokenType.Comment);

            if (Match(TokenType.OpenRoundBracket))
            {
                // TODO: parse column identifiers
            }

            if (!Match(TokenType.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            Skip(TokenType.Comment);

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            cte.Expression = select_statement();

            Skip(TokenType.Comment);

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            Skip(TokenType.Comment);

            return cte;
        }
        
        #region "EXPRESSION"
        private SyntaxNode expression()
        {
            return comparison(); // TODO: predicate() ?
        }
        private SyntaxNode comparison()
        {
            SyntaxNode left = addition();

            while (Match(TokenType.Comment, TokenType.IS,
                TokenType.Equals, TokenType.NotEquals,
                TokenType.Greater, TokenType.GreaterOrEquals,
                TokenType.Less, TokenType.LessOrEquals))
            {
                if (Previous().Type == TokenType.Comment)
                {
                    continue; // ignore
                }

                TokenType _operator = Previous().Type;

                SyntaxNode right = addition();

                left = new ComparisonOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right
                };

                if (_operator == TokenType.IS)
                {
                    check_is_expression(in left);
                }
            }

            return left;
        }
        private void check_is_expression(in SyntaxNode node)
        {
            if (node is not ComparisonOperator comparison) { return; }

            SyntaxNode expression = comparison.Expression2;

            while (expression is UnaryOperator unary)
            {
                expression = unary.Expression;
            }

            if (expression is ScalarExpression scalar)
            {
                if (scalar.Token != TokenType.NULL)
                {
                    throw new FormatException($"IS operator: NULL token expected.");
                }
            }
            else if (expression is Identifier identifier)
            {
                identifier.Token = TokenType.Type;
            }
            else
            {
                throw new FormatException($"IS operator: type identifier expected.");
            }
        }
        private SyntaxNode addition()
        {
            SyntaxNode left = multiply();

            while (Match(TokenType.Comment, TokenType.Plus, TokenType.Minus))
            {
                if (Previous().Type == TokenType.Comment)
                {
                    continue; // ignore
                }

                TokenType _operator = Previous().Type;

                SyntaxNode right = multiply();

                left = new AdditionOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode multiply()
        {
            SyntaxNode left = unary();

            while (Match(TokenType.Comment, TokenType.Star, TokenType.Divide, TokenType.Modulo))
            {
                if (Previous().Type == TokenType.Comment)
                {
                    continue; // ignore
                }

                TokenType _operator = Previous().Type;

                SyntaxNode right = unary();

                left = new MultiplyOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode unary()
        {
            if (Match(TokenType.Minus, TokenType.NOT))
            {
                TokenType _operator = Previous().Type;

                SyntaxNode expression = unary();

                return new UnaryOperator()
                {
                    Token = _operator,
                    Expression = expression
                };
            }

            return primary();
        }
        private SyntaxNode primary()
        {
            Skip(TokenType.Comment);

            if (Match(TokenType.CASE))
            {
                return case_expression();
            }
            else if (Match(TokenType.Identifier))
            {
                return identifier(TokenType.Column);
            }
            else if (Match(TokenType.Variable))
            {
                return identifier(TokenType.Variable);
            }
            else if (Match(TokenType.Boolean, TokenType.Number, TokenType.DateTime, TokenType.String, TokenType.Binary, TokenType.NULL))
            {
                return new ScalarExpression()
                {
                    Token = Previous().Type,
                    Literal = Previous().Lexeme
                };
            }
            else if (Match(TokenType.OpenRoundBracket))
            {
                SyntaxNode grouping = predicate(); // TODO: expression() ?

                if (!Match(TokenType.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket token expected.");
                }

                return new BooleanGroupExpression()
                {
                    Token = TokenType.OpenRoundBracket,
                    Expression = grouping
                };
            }

            Ignore();

            throw new FormatException($"Unknown primary expression: {Previous()}");
        }
        private SyntaxNode case_expression()
        {
            CaseExpression node = new();

            while (Match(TokenType.WHEN))
            {
                node.CASE.Add(when_expression());
            }

            if (Match(TokenType.ELSE))
            {
                node.ELSE = expression();
            }

            if (!Match(TokenType.END))
            {
                throw new FormatException($"END keyword expected.");
            }

            node.Alias = alias();

            return node;
        }
        private SyntaxNode when_expression()
        {
            WhenExpression node = new()
            {
                WHEN = predicate()
            };

            if (!Match(TokenType.THEN))
            {
                throw new FormatException($"THEN keyword expected.");
            }

            node.THEN = expression();

            return node;
        }
        #endregion

        #region "FUNCTION"
        private SyntaxNode function(TokenType token, string identifier)
        {
            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            FunctionExpression function = new()
            {
                Token = token,
                Name = identifier
            };

            function.Parameters.Add(expression());

            while (Match(TokenType.Comma))
            {
                function.Parameters.Add(expression());
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            if (Match(TokenType.OVER))
            {
                function.OVER = over_clause();
            }

            function.Alias = alias();

            return function;
        }
        private OverClause over_clause()
        {
            OverClause over = new();

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            if (Match(TokenType.PARTITION)) // optional
            {
                over.Partition = partition();
            }

            if (Match(TokenType.ORDER)) // optional
            {
                over.Order = order_clause();
            }

            if (Match(TokenType.ROWS, TokenType.RANGE))
            {
                over.FrameType = Previous().Type;

                if (Match(TokenType.BETWEEN)) // optional
                {
                    over.Preceding = window_frame(TokenType.PRECEDING);

                    if (!Match(TokenType.AND))
                    {
                        throw new FormatException("AND keyword expected.");
                    }

                    over.Following = window_frame(TokenType.FOLLOWING);
                }
                else
                {
                    over.Preceding = window_frame(TokenType.PRECEDING);
                }
            }
            
            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            return over;
        }
        private List<SyntaxNode> partition()
        {
            if (!Match(TokenType.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            List<SyntaxNode> expressions = new();

            while (Match(TokenType.Identifier, TokenType.Comma, TokenType.Comment))
            {
                ScriptToken token = Previous();

                if (token.Type == TokenType.Identifier)
                {
                    expressions.Add(identifier(TokenType.Column));
                }
            }

            return expressions;
        }
        private WindowFrame window_frame(TokenType token)
        {
            WindowFrame frame = new() { Token = token };

            if (Match(TokenType.UNBOUNDED))
            {
                frame.Extent = -1;

                if (!Match(token))
                {
                    throw new FormatException($"{token} keyword expected.");
                }
            }
            else if (Match(TokenType.CURRENT))
            {
                frame.Extent = 0;

                if (!Match(TokenType.ROW))
                {
                    throw new FormatException("ROW keyword expected.");
                }
            }
            else if (Match(TokenType.Number))
            {
                frame.Extent = int.Parse(Previous().Lexeme);

                if (!Match(token))
                {
                    throw new FormatException($"{token} keyword expected.");
                }
            }
            else
            {
                return null!;
            }

            return frame;
        }
        #endregion

        #region "SELECT STATEMENT"

        private SyntaxNode select_statement()
        {
            return union();
        }
        private SyntaxNode union()
        {
            SyntaxNode node;

            if (Match(TokenType.OpenRoundBracket))
            {
                node = select_expression();

                if (!Match(TokenType.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket expected.");
                }

                if (node is SelectStatement select)
                {
                    select.IsExpression = true;
                }
            }
            else
            {
                node = select_expression();
            }

            while (Match(TokenType.UNION))
            {
                Skip(TokenType.Comment);

                TokenType _operator = Match(TokenType.ALL) ? TokenType.UNION_ALL : TokenType.UNION;

                Skip(TokenType.Comment);

                node = new TableUnionOperator()
                {
                    Token = _operator,
                    Expression1 = node,
                    Expression2 = union()
                };
            }

            return node;
        }
        private SelectStatement select_expression()
        {
            if (!Match(TokenType.SELECT))
            {
                throw new FormatException("SELECT keyword expected.");
            }

            SelectStatement select = new();

            select_clause(in select);

            if (Match(TokenType.FROM))
            {
                select.FROM = new FromClause() { Expression = from_clause() };
            }

            if (Match(TokenType.WHERE))
            {
                select.WHERE = new WhereClause() { Expression = where_clause() };
            }

            if (Match(TokenType.GROUP))
            {
                select.GROUP = group_clause();
            }

            if (Match(TokenType.HAVING))
            {
                select.HAVING = having_clause();
            }

            if (Match(TokenType.ORDER))
            {
                select.ORDER = order_clause();
            }

            return select;
        }
        private SubqueryExpression subquery()
        {
            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            SubqueryExpression subquery = new() { Expression = union() };

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            subquery.Alias = alias();

            return subquery;
        }
        private SyntaxNode join()
        {
            SyntaxNode left = table_source();

            while (Match(TokenType.LEFT, TokenType.RIGHT, TokenType.INNER, TokenType.FULL, TokenType.CROSS))
            {
                TokenType _operator = Previous().Type;

                if (!Match(TokenType.JOIN))
                {
                    throw new FormatException("JOIN keyword expected.");
                }

                SyntaxNode right = table_source();

                if (!Match(TokenType.ON))
                {
                    throw new FormatException("ON keyword expected.");
                }

                left = new TableJoinOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right,
                    ON = new OnClause() { Expression = where_clause() }
                };
            }

            return left;
        }
        
        #region "FROM CLAUSE"

        private SyntaxNode from_clause()
        {
            return join();
        }
        private TableSource table_source()
        {
            SyntaxNode expression = null!;

            if (Match(TokenType.Identifier))
            {
                expression = identifier(TokenType.Table);
            }
            else if (Check(TokenType.OpenRoundBracket))
            {
                expression = subquery();
            }

            if (expression == null)
            {
                throw new FormatException("Identifier or Subquery expected.");
            }

            TableSource table = new() { Expression = expression };

            if (!Match(TokenType.WITH))
            {
                return table;
            }

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException($"Open round bracket expected.");
            }

            while (Match(TokenType.Comma, TokenType.NOLOCK, TokenType.ROWLOCK, TokenType.READPAST,
                TokenType.READCOMMITTEDLOCK, TokenType.SERIALIZABLE, TokenType.UPDLOCK))
            {
                if (Previous().Type != TokenType.Comma)
                {
                    table.Hints.Add(Previous().Type);
                }
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException($"Close round bracket expected.");
            }

            return table;
        }
        
        #endregion

        #region "WHERE CLAUSE"
        private SyntaxNode where_clause()
        {
            return predicate();
        }
        private SyntaxNode predicate()
        {
            return or(); // TODO: IN
        }
        private SyntaxNode or()
        {
            SyntaxNode left = and();

            while (Match(TokenType.OR))
            {
                SyntaxNode right = and();

                left = new BooleanBinaryOperator()
                {
                    Token = TokenType.OR,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode and()
        {
            SyntaxNode left = not();

            while (Match(TokenType.AND))
            {
                SyntaxNode right = not();

                left = new BooleanBinaryOperator()
                {
                    Token = TokenType.AND,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode not()
        {
            if (Match(TokenType.NOT))
            {
                SyntaxNode unary = not();

                return new BooleanUnaryOperator()
                {
                    Token = TokenType.NOT,
                    Expression = unary
                };
            }

            return expression();
        }
        #endregion

        #region "GROUP BY ... HAVING CLAUSE"
        private GroupClause group_clause()
        {
            if (!Match(TokenType.BY, TokenType.ON))
            {
                throw new FormatException("BY keyword expected.");
            }

            GroupClause group = new();

            while (Match(TokenType.Identifier, TokenType.Comma, TokenType.Comment))
            {
                ScriptToken token = Previous();

                if (token.Type == TokenType.Identifier)
                {
                    group.Expressions.Add(identifier(TokenType.Column));
                }
            }

            return group;
        }
        private HavingClause having_clause()
        {
            return new HavingClause()
            {
                Expression = predicate() // see WHERE clause
            };
        }
        #endregion

        #region "ORDER BY ... OFFSET ... FETCH CLAUSE"
        private OrderClause order_clause()
        {
            if (!Match(TokenType.BY, TokenType.ON))
            {
                throw new FormatException("BY keyword expected.");
            }

            OrderClause order = new();

            while (Match(TokenType.Identifier, TokenType.Comma, TokenType.Comment))
            {
                ScriptToken token = Previous();

                if (token.Type == TokenType.Identifier)
                {
                    SyntaxNode column = identifier(TokenType.Column);

                    TokenType sort_order = TokenType.ASC;

                    if (Match(TokenType.ASC, TokenType.DESC))
                    {
                        sort_order = Previous().Type;
                    }

                    order.Expressions.Add(new OrderExpression()
                    {
                        Token = sort_order,
                        Expression = column
                    });
                }
            }

            if (Match(TokenType.OFFSET))
            {
                order.Offset = expression();

                if (!Match(TokenType.ROW, TokenType.ROWS))
                {
                    throw new FormatException("ROW or ROWS keyword expected.");
                }
            }
            
            if (Match(TokenType.FETCH))
            {
                if (!Match(TokenType.FIRST, TokenType.NEXT))
                {
                    throw new FormatException("FIRST or NEXT keyword expected.");
                }

                order.Fetch = expression();

                if (!Match(TokenType.ROW, TokenType.ROWS))
                {
                    throw new FormatException("ROW or ROWS keyword expected.");
                }

                if (!Match(TokenType.ONLY))
                {
                    throw new FormatException("ROW or ROWS keyword expected.");
                }
            }

            return order;
        }
        #endregion

        #region "SELECT CLAUSE"

        private void select_clause(in SelectStatement select)
        {
            Skip(TokenType.Comment);

            top(in select);

            Skip(TokenType.Comment);

            select.SELECT.Add(expression());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                select.SELECT.Add(expression());

                Skip(TokenType.Comment);
            }
        }
        private void top(in SelectStatement select)
        {
            if (!Match(TokenType.TOP))
            {
                return;
            }

            if (Match(TokenType.Number))
            {
                select.TOP = new ScalarExpression()
                {
                    Token = TokenType.Number,
                    Literal = Previous().Lexeme
                };
                return;
            }

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException($"Open round bracket expected.");
            }

            if (Match(TokenType.Variable))
            {
                select.TOP = identifier(TokenType.Variable);
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException($"Close round bracket expected.");
            }
        }
        private SyntaxNode star()
        {
            return new StarExpression();
        }
        private SyntaxNode identifier(TokenType context)
        {
            string value = Previous().Lexeme;

            if (context == TokenType.Table || context == TokenType.Column)
            {
                if (ScriptHelper.IsFunction(value, out TokenType token))
                {
                    return function(token, value);
                }
            }

            Identifier identifier = new()
            {
                Token = Previous().Type,
                Value = value,
                Alias = alias()
            };

            if (context == TokenType.Table ||
                context == TokenType.Column ||
                context == TokenType.Variable)
            {
                identifier.Token = context;
            }
            
            return identifier;
        }
        private string alias()
        {
            if (Match(TokenType.AS))
            {
                if (!Match(TokenType.Identifier))
                {
                    throw new FormatException("Alias expected.");
                }
                else
                {
                    return Previous().Lexeme;
                }
            }
            else if (Match(TokenType.Identifier))
            {
                return Previous().Lexeme;
            }

            return string.Empty;
        }

        #endregion

        #endregion

        #region "DELETE STATEMENT"

        private DeleteStatement delete_statement()
        {
            if (!Match(TokenType.DELETE))
            {
                throw new FormatException("DELETE keyword expected.");
            }

            DeleteStatement delete = new();

            if (Match(TokenType.FROM)) { /* do nothing - optional */ }

            delete.TARGET = table_source();

            if (Match(TokenType.OUTPUT))
            {
                delete.OUTPUT = output_clause();
            }

            if (Match(TokenType.FROM))
            {
                delete.FROM = new FromClause() { Expression = from_clause() };
            }

            if (Match(TokenType.WHERE))
            {
                delete.WHERE = new WhereClause() { Expression = where_clause() };
            }

            return delete;
        }
        private OutputClause output_clause()
        {
            OutputClause output = new();

            while (Match(TokenType.Comma, TokenType.Comment,
                TokenType.Star, TokenType.Identifier))
            {
                ScriptToken token = Previous();

                if (token.Type == TokenType.Star)
                {
                    output.Expressions.Add(star());
                }
                else if (token.Type == TokenType.Identifier)
                {
                    output.Expressions.Add(identifier(TokenType.Column));
                }
            }

            return output;
        }

        #endregion
    }
}