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
                return declare();
            }
            else if (Match(TokenType.WITH))
            {
                return statement_with_cte();
            }
            else if (Check(TokenType.SELECT))
            {
                return select_statement();
            }
            //else if (Check(TokenType.DELETE))
            //{
            //    return delete_statement();
            //}
            else if (Match(TokenType.EndOfStatement))
            {
                return null;
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
                // do nothing - optional
            }

            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Variable data type identifier expected.");
            }
            else
            {
                declare.Type = type();
            }

            if (Match(TokenType.Equals))
            {
                if (!Match(TokenType.String, TokenType.Boolean, TokenType.Number, TokenType.Entity))
                {
                    throw new FormatException("Scalar expression expected.");
                }
                else
                {
                    declare.Initializer = scalar();
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
                return new SelectStatement()
                {
                    Select = union(),
                    CommonTables = root
                };
            }

            //if (Check(TokenType.DELETE))
            //{
            //    DeleteStatement delete = delete_statement();
            //    delete.CommonTables = root;
            //    return delete;
            //}

            throw new FormatException("SELECT statement expected.");
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

            //if (Match(TokenType.OpenRoundBracket))
            //{
            //    // TODO: parse cte column identifiers
            //}

            if (!Match(TokenType.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            Skip(TokenType.Comment);

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            cte.Expression = union();

            Skip(TokenType.Comment);

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            Skip(TokenType.Comment);

            return cte;
        }

        #region "SELECT STATEMENT"
        private SyntaxNode select_statement()
        {
            return new SelectStatement()
            {
                Select = union()
            };
        }
        private SyntaxNode union()
        {
            SyntaxNode node;

            if (Match(TokenType.OpenRoundBracket))
            {
                node = select();

                if (!Match(TokenType.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket expected.");
                }
            }
            else
            {
                node = select();
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
        private SelectExpression select()
        {
            if (!Match(TokenType.SELECT))
            {
                throw new FormatException("SELECT keyword expected.");
            }

            SelectExpression select = new();

            select_clause(in select);

            if (Match(TokenType.FROM)) { select.From = from_clause(); }
            if (Match(TokenType.WHERE)) { select.Where = where_clause(); }
            if (Match(TokenType.GROUP)) { select.Group = group_clause(); }
            if (Match(TokenType.HAVING)) { select.Having = having_clause(); }
            if (Match(TokenType.ORDER)) { select.Order = order_clause(); }

            return select;
        }
        private SyntaxNode table()
        {
            //TODO: analyse identifier if it is table variable, temporary table or table function

            //if (ScriptHelper.IsFunction(value, out TokenType token))
            //{
            //    return function(token, value);
            //}

            if (Match(TokenType.Identifier))
            {
                string identifier = Previous().Lexeme;

                return new TableReference()
                {
                    Alias = alias(),
                    Identifier = identifier
                };
            }

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            TableExpression table = new() { Expression = union() };

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            table.Alias = alias();

            return table;
        }
        private SyntaxNode join()
        {
            SyntaxNode left = table();

            while (Match(TokenType.LEFT, TokenType.RIGHT, TokenType.INNER, TokenType.FULL, TokenType.CROSS))
            {
                TokenType _operator = Previous().Type;

                if (!Match(TokenType.JOIN))
                {
                    throw new FormatException("JOIN keyword expected.");
                }

                SyntaxNode right = table();

                if (!Match(TokenType.ON))
                {
                    throw new FormatException("ON keyword expected.");
                }

                left = new TableJoinOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right,
                    On = on_clause()
                };
            }

            return left;
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
        private SyntaxNode star()
        {
            return new StarExpression();
        }
        private ColumnExpression column()
        {
            return new ColumnExpression()
            {
                Expression = expression(),
                Alias = alias()
            };
        }
        private void select_clause(in SelectExpression select)
        {
            Skip(TokenType.Comment);

            top(in select);

            Skip(TokenType.Comment);

            select.Select.Add(column());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                select.Select.Add(column());

                Skip(TokenType.Comment);
            }
        }
        private void top(in SelectExpression select)
        {
            if (!Match(TokenType.TOP))
            {
                return;
            }

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException($"Open round bracket expected.");
            }

            select.Top = new TopClause()
            {
                Expression = expression()
            };

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException($"Close round bracket expected.");
            }
        }

        private FromClause from_clause() { return new FromClause() { Expression = join() }; }
        private OnClause on_clause() { return new OnClause() { Expression = predicate() }; }
        private WhereClause where_clause() { return new WhereClause() { Expression = predicate() }; }
        private HavingClause having_clause() { return new HavingClause() { Expression = predicate() }; }
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

                left = new BinaryOperator()
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

                left = new BinaryOperator()
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

                return new UnaryOperator()
                {
                    Token = TokenType.NOT,
                    Expression = unary
                };
            }

            return expression();
        }

        private GroupClause group_clause()
        {
            if (!Match(TokenType.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            GroupClause group = new();

            Skip(TokenType.Comment);

            group.Expressions.Add(expression());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                group.Expressions.Add(expression());

                Skip(TokenType.Comment);
            }

            return group;
        }
        private OrderClause order_clause()
        {
            if (!Match(TokenType.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            OrderClause order = new();

            Skip(TokenType.Comment);

            order.Expressions.Add(order_expression());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);
                
                order.Expressions.Add(order_expression());

                Skip(TokenType.Comment);
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
        private OrderExpression order_expression()
        {
            SyntaxNode column = expression();

            TokenType sort_order = TokenType.ASC;

            if (Match(TokenType.ASC, TokenType.DESC))
            {
                sort_order = Previous().Type;
            }

            return new OrderExpression()
            {
                Token = sort_order,
                Expression = column
            };
        }
        #endregion

        #region "EXPRESSION"
        private SyntaxNode expression()
        {
            return comparison();
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

                if (_operator == TokenType.IS)
                {
                    return new ComparisonOperator()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = is_right_operand()
                    };
                }
                else
                {
                    left = new ComparisonOperator()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = addition()
                    };
                }
            }

            return left;
        }
        private SyntaxNode is_right_operand()
        {
            if (Match(TokenType.NOT))
            {
                UnaryOperator unary = new()
                {
                    Token = TokenType.NOT
                };

                if (Match(TokenType.NULL))
                {
                    unary.Expression = scalar();
                }
                else if (Match(TokenType.Identifier))
                {
                    unary.Expression = type();
                }
                else
                {
                    throw new FormatException($"NULL or type identifier expected.");
                }

                return unary;
            }
            else if (Match(TokenType.NULL))
            {
                return scalar();
            }
            else if (Match(TokenType.Identifier))
            {
                return type();
            }

            throw new FormatException($"NOT token, NULL or type identifier expected.");
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

            return terminal();
        }
        private SyntaxNode terminal()
        {
            Skip(TokenType.Comment);

            if (Match(TokenType.Identifier))
            {
                return identifier();
            }
            else if (Match(TokenType.Variable))
            {
                return variable();
            }
            else if (Match(TokenType.Boolean, TokenType.Number, TokenType.DateTime,
                TokenType.String, TokenType.Binary, TokenType.NULL, TokenType.Entity))
            {
                return scalar();
            }
            if (Match(TokenType.CASE))
            {
                return case_expression();
            }
            else if (Match(TokenType.OpenRoundBracket))
            {
                SyntaxNode grouping = expression();

                if (!Match(TokenType.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket token expected.");
                }

                return new GroupOperator() { Expression = grouping };
            }

            Ignore();

            throw new FormatException($"Unknown expression: {Previous()}");
        }
        private TypeIdentifier type()
        {
            return new TypeIdentifier()
            {
                Identifier = Previous().Lexeme
            };
        }
        private SyntaxNode scalar()
        {
            ScalarExpression scalar = new()
            {
                Token = Previous().Type,
                Literal = Previous().Lexeme
            };

            if (scalar.Token == TokenType.String && scalar.Literal.Length >= 12)
            {
                int start = 1;
                int length = scalar.Literal.Length - 2;

                string value = scalar.Literal.Trim().Substring(start, length); // remove leading and trailing ' and "

                if (Guid.TryParse(value, out Guid _))
                {
                    scalar.Token = TokenType.Uuid;
                }
                else if (DateTime.TryParse(value, out DateTime _))
                {
                    scalar.Token = TokenType.DateTime;
                }
            }

            return scalar;
        }
        private SyntaxNode variable()
        {
            return new VariableReference() { Identifier = Previous().Lexeme };
        }
        private SyntaxNode identifier()
        {
            string identifier = Previous().Lexeme;

            if (ScriptHelper.IsFunction(identifier, out TokenType token))
            {
                return function(token, identifier);
            }

            return new ColumnReference() { Identifier = identifier };
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

            return node;
        }
        private WhenClause when_expression()
        {
            WhenClause node = new()
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
                function.Over = over_clause();
            }

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
        private PartitionClause partition()
        {
            if (!Match(TokenType.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            PartitionClause clause = new();

            clause.Columns.Add(expression());

            while (Match(TokenType.Comma))
            {
                clause.Columns.Add(expression());
            }

            return clause;
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

        #region "DELETE STATEMENT"
        private TableSource table_source()
        {
            SyntaxNode expression = null!;

            if (Match(TokenType.Identifier))
            {
                expression = table();
            }
            else if (Check(TokenType.OpenRoundBracket))
            {
                expression = table();
            }

            if (expression == null)
            {
                throw new FormatException("Identifier or Subquery expected.");
            }

            TableSource source = new() { Expression = expression };

            if (!Match(TokenType.WITH))
            {
                return source;
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
                    source.Hints.Add(Previous().Type);
                }
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException($"Close round bracket expected.");
            }

            return source;
        }
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
                delete.FROM = from_clause();
            }

            if (Match(TokenType.WHERE))
            {
                delete.WHERE = where_clause();
            }

            return delete;
        }
        private OutputClause output_clause()
        {
            OutputClause output = new();

            while (Match(TokenType.Comma, TokenType.Comment, TokenType.Star, TokenType.Identifier))
            {
                ScriptToken token = Previous();

                if (token.Type == TokenType.Star)
                {
                    output.Expressions.Add(new ColumnExpression()
                    {
                        Expression = star()
                    });
                }
                else if (token.Type == TokenType.Identifier)
                {
                    output.Expressions.Add(column());
                }
            }

            return output;
        }
        #endregion
    }
}