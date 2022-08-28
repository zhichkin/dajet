using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public struct ScriptParser : IDisposable
    {
        private int _current = 0;
        private ScriptToken? _token = null;
        private List<ScriptToken>? _tokens = null;
        private List<ScriptToken>? _ignore = null; // test and debug purposes
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
                SyntaxNode? node;

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
            else if (Match(TokenType.SELECT))
            {
                return select_statement();
            }
            else if (Match(TokenType.WITH))
            {
                return statement_with_cte();
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

            if (Match(TokenType.SELECT))
            {
                SelectStatement select = select_statement();

                select.CTE = root;

                return select;
            }

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

            cte.Expression = select_definition(TokenType.SELECT); // !? TokenType.CTE

            Skip(TokenType.Comment);

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            Skip(TokenType.Comment);

            return cte;
        }
        private SelectStatement select_definition(TokenType type)
        {
            SelectStatement select = new()
            {
                Token = type
            };

            if (!Match(TokenType.SELECT))
            {
                throw new FormatException("SELECT keyword expected.");
            }
            select_clause(in select);

            if (!Match(TokenType.FROM))
            {
                throw new FormatException("FROM keyword expected.");
            }
            select.FROM = new FromClause() { Expression = from_clause() };

            if (Match(TokenType.WHERE))
            {
                select.WHERE = new WhereClause() { Expression = where_clause() };
            }

            return select;
        }

        #region "SELECT STATEMENT"

        private SelectStatement select_statement()
        {
            SelectStatement select = new() { Token = TokenType.SELECT };

            select_clause(in select);

            while (!EndOfStream() && !Check(TokenType.EndOfStatement))
            {
                if (Match(TokenType.FROM))
                {
                    select.FROM = new FromClause() { Expression = from_clause() };
                }
                else if (Match(TokenType.WHERE))
                {
                    select.WHERE = new WhereClause() { Expression = where_clause() };
                }
                else
                {
                    Ignore();
                }
            }

            if (!Match(TokenType.EndOfStatement))
            {
                throw new FormatException("End of statement expected.");
            }

            return select;
        }

        #region "FROM CLAUSE"

        private SyntaxNode from_clause()
        {
            return join();
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
        private SyntaxNode table_source()
        {
            if (Match(TokenType.Identifier))
            {
                return identifier(TokenType.Table);
            }
            else if (Match(TokenType.OpenRoundBracket))
            {
                return subquery();
            }

            throw new FormatException("Identifier or subquery expected.");
        }
        private SyntaxNode subquery()
        {
            if (!Match(TokenType.SELECT))
            {
                throw new FormatException("SELECT keyword expected.");
            }

            SelectStatement select = new() { Token = TokenType.SELECT };

            select_clause(in select);

            while (!EndOfStream() && !Check(TokenType.CloseRoundBracket))
            {
                if (Match(TokenType.FROM))
                {
                    select.FROM = new FromClause() { Expression = from_clause() };
                }
                else if (Match(TokenType.WHERE))
                {
                    select.WHERE = new WhereClause() { Expression = where_clause() };
                }
                else
                {
                    Ignore();
                }
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("End of subquery expected.");
            }

            return new SubqueryExpression()
            {
                Token = TokenType.SELECT,
                QUERY = select,
                Alias = alias()
            };
        }

        #endregion

        #region "WHERE CLAUSE"
        private SyntaxNode where_clause()
        {
            return predicate();
        }
        private SyntaxNode predicate()
        {
            return or();
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

            return comparison();
        }
        private SyntaxNode comparison()
        {
            SyntaxNode left = primary();

            while (Match(TokenType.Comment,
                TokenType.Equals, TokenType.NotEquals,
                TokenType.Greater, TokenType.GreaterOrEquals,
                TokenType.Less, TokenType.LessOrEquals))
            {
                if (Previous().Type == TokenType.Comment)
                {
                    continue; // ignore
                }

                TokenType _operator = Previous().Type;

                SyntaxNode right = primary();

                left = new ComparisonOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode primary()
        {
            while (Match(TokenType.Comment))
            {
                // ignore
            }

            if (Match(TokenType.Identifier))
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
                SyntaxNode grouping = predicate();

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
        #endregion
        
        #region "SELECT CLAUSE"

        private void select_clause(in SelectStatement select)
        {
            while (!EndOfStream() && Match(TokenType.Star, TokenType.Identifier, TokenType.Comma, TokenType.Comment))
            {
                ScriptToken token = Previous();

                if (token.Type == TokenType.Star)
                {
                    select.SELECT.Add(star());
                }
                else if (token.Type == TokenType.Identifier)
                {
                    select.SELECT.Add(identifier(TokenType.Column));
                }
            }
        }
        private SyntaxNode star()
        {
            return new StarExpression();
        }
        private SyntaxNode identifier(TokenType context)
        {
            Identifier identifier = new()
            {
                Token = Previous().Type,
                Value = Previous().Lexeme,
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
    }
}