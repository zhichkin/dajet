using DaJet.Metadata;
using DaJet.Scripting.Model;
using static System.Formats.Asn1.AsnWriter;

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
                if (!Match(TokenType.Boolean, TokenType.Number, TokenType.String, TokenType.Binary, TokenType.Entity))
                {
                    throw new FormatException("Scalar initializer expression expected.");
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
            else if (Match(TokenType.CREATE))
            {
                return create_statement();
            }
            else if (Check(TokenType.SELECT))
            {
                return select_statement();
            }
            else if (Check(TokenType.INSERT))
            {
                return insert_statement();
            }
            else if (Check(TokenType.UPDATE))
            {
                return update_statement();
            }
            else if (Check(TokenType.DELETE))
            {
                return delete_statement();
            }
            else if (Check(TokenType.UPSERT))
            {
                return upsert_statement();
            }
            else if (Match(TokenType.EndOfStatement))
            {
                return null;
            }

            Ignore();

            throw new FormatException($"Unknown statement: {Previous()}");
        }

        #region "CREATE TABLE STATEMENT"
        private SyntaxNode create_statement()
        {
            if (Match(TokenType.TYPE))
            {
                return create_type();
            }

            TokenType token = TokenType.TABLE;

            if (Match(TokenType.COMPUTED, TokenType.TEMPORARY))
            {
                token = Previous().Type;
            }

            if (!Match(TokenType.TABLE))
            {
                throw new FormatException("TABLE keyword expected.");
            }

            if (Match(TokenType.VARIABLE))
            {
                token = Previous().Type;
            }

            if (token == TokenType.TABLE)
            {
                return create_table();
            }

            if (!Check(TokenType.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            if (token == TokenType.COMPUTED)
            {
                return statement_with_cte();
            }
            else if (token == TokenType.VARIABLE)
            {
                return table_variable();
            }
            else if (token == TokenType.TEMPORARY)
            {
                return temporary_table();
            }

            throw new FormatException("Invalid CREATE TABLE statement.");
        }
        private SyntaxNode create_table()
        {
            if (!Match(TokenType.Identifier)) { throw new FormatException("Table identifier expected."); }

            string identifier = Previous().Lexeme;

            if (!Match(TokenType.OF))
            {
                throw new FormatException("OF keyword expected.");
            }

            if (!Match(TokenType.Identifier)) { throw new FormatException("Type identifier expected."); }

            return new CreateTableStatement()
            {
                Name = identifier,
                Type = Previous().Lexeme
            };
        }
        private SyntaxNode table_variable()
        {
            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            string identifier = Previous().Lexeme;

            if (!Match(TokenType.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            bool expect_close = Match(TokenType.CloseRoundBracket);

            TableVariableExpression table = new()
            {
                Name = identifier,
                Expression = union()
            };

            if (expect_close && !Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("CREATE TABLE: close round bracket expected.");
            }

            return table;
        }
        private SyntaxNode temporary_table()
        {
            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            string identifier = Previous().Lexeme;

            if (!Match(TokenType.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            bool expect_close = Match(TokenType.CloseRoundBracket);

            TemporaryTableExpression table = new()
            {
                Name = identifier,
                Expression = union()
            };

            if (expect_close && !Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("CREATE TABLE: close round bracket expected.");
            }

            return table;
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
            else if (Check(TokenType.INSERT))
            {
                InsertStatement insert = insert_statement();
                insert.CommonTables = root;
                return insert;
            }
            else if (Check(TokenType.UPDATE))
            {
                UpdateStatement update = update_statement();
                update.CommonTables = root;
                return update;
            }
            else if (Check(TokenType.DELETE))
            {
                DeleteStatement delete = delete_statement();
                delete.CommonTables = root;
                return delete;
            }
            else if (Check(TokenType.UPSERT))
            {
                UpsertStatement upsert = upsert_statement();
                upsert.CommonTables = root;
                return upsert;
            }

            throw new FormatException("Statement expected.");
        }
        private CommonTableExpression cte()
        {
            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            CommonTableExpression cte = new()
            {
                Name = Previous().Lexeme
            };

            Skip(TokenType.Comment);

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
        #endregion

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
            SyntaxNode node = union_operator();

            if (node is not TableUnionOperator _union)
            {
                return node;
            }

            SyntaxNode bottom = _union.Expression2;

            while (bottom is TableUnionOperator next)
            {
                bottom = next.Expression2;
            }

            if (bottom is not SelectExpression select)
            {
                throw new FormatException("UNION: SELECT expression expected.");
            }

            if (select.Order is not null)
            {
                _union.Order = select.Order;

                select.Order = null;
            }
            else if (Previous().Type == TokenType.CloseRoundBracket)
            {
                if (Match(TokenType.ORDER)) { _union.Order = order_clause(); }
            }
            
            return _union;
        }
        private SyntaxNode union_operator()
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

            if (Match(TokenType.UNION))
            {
                if (node is SelectExpression select && select.Order is not null)
                {
                    throw new FormatException("UNION: unexpected ORDER keyword.");
                }

                Skip(TokenType.Comment);

                TokenType _operator = Match(TokenType.ALL) ? TokenType.UNION_ALL : TokenType.UNION;

                Skip(TokenType.Comment);

                node = new TableUnionOperator()
                {
                    Token = _operator,
                    Expression1 = node,
                    Expression2 = union_operator()
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

            Skip(TokenType.Comment);
            select_clause(in select);
            Skip(TokenType.Comment);
            if (Match(TokenType.FROM)) { select.From = from_clause(); }
            Skip(TokenType.Comment);
            if (Match(TokenType.WHERE)) { select.Where = where_clause(); }
            Skip(TokenType.Comment);
            if (Match(TokenType.GROUP)) { select.Group = group_clause(); }
            Skip(TokenType.Comment);
            if (Match(TokenType.HAVING)) { select.Having = having_clause(); }
            Skip(TokenType.Comment);
            if (Match(TokenType.ORDER)) { select.Order = order_clause(); }
            Skip(TokenType.Comment);

            return select;
        }
        private SyntaxNode table()
        {
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

            if (!Check(TokenType.AS)) { throw new FormatException("Alias expected."); }

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
        private ColumnExpression column()
        {
            if (Match(TokenType.Star))
            {
                return new ColumnExpression()
                {
                    Expression = new StarExpression(),
                    Alias = alias()
                };
            }

            return new ColumnExpression()
            {
                Expression = expression(),
                Alias = alias()
            };
        }
        private void select_clause(in SelectExpression select)
        {
            Skip(TokenType.Comment);
            select.Distinct = Match(TokenType.DISTINCT);
            Skip(TokenType.Comment);

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

            bool expect_close = Match(TokenType.OpenRoundBracket);

            select.Top = new TopClause()
            {
                Expression = expression()
            };

            if (expect_close && !Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException($"TOP clause: close round bracket expected.");
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
            TypeIdentifier type = new()
            {
                Identifier = Previous().Lexeme
            };

            if (Match(TokenType.OpenRoundBracket))
            {
                if (!Match(TokenType.Number)) { throw new FormatException("Number literal expected."); }
                if (!int.TryParse(Previous().Lexeme, out int qualifier1)) { throw new FormatException("Number literal expected."); }

                type.Qualifier1 = qualifier1;

                if (Match(TokenType.Comma))
                {
                    if (!Match(TokenType.Number)) { throw new FormatException("Number literal expected."); }
                    if (!int.TryParse(Previous().Lexeme, out int qualifier2)) { throw new FormatException("Number literal expected."); }

                    type.Qualifier2 = qualifier2;
                }

                if (!Match(TokenType.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }
            }

            return type;
        }
        private SyntaxNode scalar()
        {
            ScalarExpression scalar = new()
            {
                Token = Previous().Type,
                Literal = Previous().Lexeme
            };

            if (scalar.Token == TokenType.String && scalar.Literal.Length >= 10)
            {
                if (Guid.TryParse(scalar.Literal, out Guid _))
                {
                    scalar.Token = TokenType.Uuid;
                }
                else if (DateTime.TryParse(scalar.Literal, out DateTime _))
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

            if (Match(TokenType.CloseRoundBracket))
            {
                // the function does not have any parameters
            }
            else
            {
                function.Parameters.Add(expression());

                while (Match(TokenType.Comma))
                {
                    function.Parameters.Add(expression());
                }

                if (!Match(TokenType.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket expected.");
                }
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

        #region "INSERT STATEMENT"
        private InsertStatement insert_statement()
        {
            if (!Match(TokenType.INSERT))
            {
                throw new FormatException("INSERT keyword expected.");
            }

            if (Match(TokenType.INTO)) { /* do nothing - optional */ }

            InsertStatement insert = new()
            {
                Target = table_identifier()
            };

            if (Match(TokenType.FROM))
            {
                insert.Source = table();
            }
            else if (Check(TokenType.SELECT))
            {
                insert.Source = union();
            }
            else
            {
                throw new FormatException("INSERT: table source expression expected.");
            }
            
            return insert;
        }
        private SyntaxNode values()
        {
            if (!Match(TokenType.VALUES))
            {
                throw new FormatException("VALUES keyword expected.");
            }
            
            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            ValuesExpression values = new();

            values.Values.Add(expression());

            while (Match(TokenType.Comma))
            {
                values.Values.Add(expression());
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            return values;
        }
        private TableReference table_identifier()
        {
            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("INSERT: target identifier expected.");
            }
            
            return new TableReference() { Identifier = Previous().Lexeme };
        }
        private ColumnReference column_identifier()
        {
            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Column identifier expected.");
            }

            return new ColumnReference() { Identifier = Previous().Lexeme };
        }
        #endregion

        #region "UPDATE STATEMENT"
        private UpdateStatement update_statement()
        {
            if (!Match(TokenType.UPDATE)) { throw new FormatException("UPDATE keyword expected."); }

            UpdateStatement update = new()
            {
                Target = table_identifier()
            };

            if (Match(TokenType.FROM)) // FROM-WHERE-SET | FROM-SET-WHERE
            {
                update.Source = table();
                if (Match(TokenType.WHERE))
                {
                    update.Where = where_clause();
                    if (Match(TokenType.SET)) { update.Set = set_clause(update); }
                }
                else if (Match(TokenType.SET))
                {
                    update.Set = set_clause(update);
                    if (Match(TokenType.WHERE)) { update.Where = where_clause(); }
                }
            }
            else if (Match(TokenType.WHERE)) // WHERE-FROM-SET | WHERE-SET-FROM
            {
                update.Where = where_clause();
                if (Match(TokenType.FROM))
                {
                    update.Source = table();
                    if (Match(TokenType.SET)) { update.Set = set_clause(update); }
                }
                else if (Match(TokenType.SET))
                {
                    update.Set = set_clause(update);
                    if (Match(TokenType.FROM)) { update.Source = table(); }
                }
            }
            else if (Match(TokenType.SET)) // SET-FROM-WHERE | SET-WHERE-FROM
            {
                update.Set = set_clause(update);
                if (Match(TokenType.FROM))
                {
                    update.Source = table();
                    if (Match(TokenType.WHERE)) { update.Where = where_clause(); }
                }
                else if (Match(TokenType.WHERE))
                {
                    update.Where = where_clause();
                    if (Match(TokenType.FROM)) { update.Source = table(); }
                }
            }

            // The WHERE and FROM clauses are optional
            if (update.Set is null) { throw new FormatException("UPDATE: SET keyword expected."); }

            return update;
        }
        private SetExpression set_expression()
        {
            SetExpression set = new()
            {
                Column = column_identifier()
            };

            if (!Match(TokenType.Equals))
            {
                throw new FormatException("Assignment operator expected.");
            }

            set.Initializer = expression();

            return set;
        }
        private SetClause set_clause(SyntaxNode parent)
        {
            SetClause clause = new() { Parent = parent };

            clause.Expressions.Add(set_expression());

            while (Match(TokenType.Comma))
            {
                clause.Expressions.Add(set_expression());
            }

            return clause;
        }
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

            delete.Target = table_identifier();

            if (Match(TokenType.WHERE))
            {
                delete.Where = where_clause();
            }

            return delete;
        }
        private OutputClause output_clause()
        {
            OutputClause output = new();

            Skip(TokenType.Comment);
            output.Columns.Add(column());
            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);
                output.Columns.Add(column());
                Skip(TokenType.Comment);
            }

            return output;
        }
        #endregion

        #region "UPSERT STATEMENT"
        private UpsertStatement upsert_statement()
        {
            if (!Match(TokenType.UPSERT))
            {
                throw new FormatException("UPSERT keyword expected.");
            }

            UpsertStatement upsert = new()
            {
                Target = table_identifier()
            };

            bool ignore = Match(TokenType.IGNORE);
            bool update = Match(TokenType.UPDATE);
            if (ignore && !update)
            {
                throw new FormatException("UPSERT: UPDATE keyword is missing.");
            }
            else if (update && !ignore)
            {
                throw new FormatException("UPSERT: IGNORE keyword is missing.");
            }
            upsert.IgnoreUpdate = ignore && update;

            bool from = Match(TokenType.FROM);
            if (from) { upsert.Source = table(); }

            if (Match(TokenType.WHERE)) { upsert.Where = where_clause(); }
            else { throw new FormatException("UPSERT: WHERE keyword expected."); }

            if (Match(TokenType.SET)) { upsert.Set = set_clause(upsert); }
            else { throw new FormatException("UPSERT: SET keyword expected."); }

            if (from)
            {
                if (Check(TokenType.FROM))
                {
                    throw new FormatException("UPSERT: FROM clause is used twice.");
                }
            }
            else if (Match(TokenType.FROM)) { upsert.Source = table(); }
            else { throw new FormatException("UPSERT: FROM keyword expected."); }

            return upsert;
        }
        #endregion

        #region "CREATE TYPE STATEMENT"
        private SyntaxNode create_type()
        {
            if (Match(TokenType.ENTITY))
            {
                // SYSTEM TYPE ENTITY DEFINITION
            }
            else if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Type identifier expected.");
            }

            CreateTypeStatement statement = new()
            {
                Name = Previous().Lexeme,
                BaseType = base_type(),
                DropColumns = drop_columns(),
                AlterColumns = alter_columns()
            };

            if (Match(TokenType.PRIMARY))
            {
                if (!Match(TokenType.KEY)) { throw new FormatException("KEY keyword expected."); }

                statement.PrimaryKey = column_identifiers();
            }

            if (!Match(TokenType.OpenRoundBracket)) { throw new FormatException("Open round bracket expected."); }

            statement.Columns.Add(column_definition());

            while (Match(TokenType.Comma))
            {
                statement.Columns.Add(column_definition());
            }

            if (!Match(TokenType.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }

            return statement;

            //throw new FormatException("Invalid CREATE TYPE statement.");
        }
        private List<string> column_identifiers()
        {
            if (!Match(TokenType.Identifier)) { throw new FormatException("Column identifier expected."); }

            List<string> columns = new()
            {
                Previous().Lexeme
            };

            while (Match(TokenType.Comma))
            {
                if (!Match(TokenType.Identifier)) { throw new FormatException("Column identifier expected."); }

                columns.Add(Previous().Lexeme);
            }

            return columns;
        }
        private string base_type()
        {
            if (!Match(TokenType.AS)) { return null; }

            if (Match(TokenType.ENTITY)) { return Previous().Lexeme; }

            if (!Match(TokenType.Identifier)) { throw new FormatException("Base type identifier expected."); }

            return Previous().Lexeme;
        }
        private List<string> drop_columns()
        {
            if (!Match(TokenType.DROP)) { return null; }

            if (!Match(TokenType.COLUMN)) { throw new FormatException("COLUMN keyword expected."); }

            return column_identifiers();
        }
        private List<ColumnDefinition> alter_columns()
        {
            if (!Match(TokenType.ALTER)) { return null; }

            if (!Match(TokenType.COLUMN)) { throw new FormatException("COLUMN keyword expected."); }

            List<ColumnDefinition> columns = new()
            {
                column_definition()
            };

            while (Match(TokenType.ALTER))
            {
                if (!Match(TokenType.COLUMN)) { throw new FormatException("COLUMN keyword expected."); }

                columns.Add(column_definition());
            }

            return columns;
        }
        private ColumnDefinition column_definition()
        {
            if (!Match(TokenType.Identifier)) { throw new FormatException("Column identifier expected."); }

            ColumnDefinition column = new()
            {
                Name = Previous().Lexeme
            };

            if (!Match(TokenType.Identifier)) { throw new FormatException("Data type identifier expected."); }

            column.Type = type();

            column.IsNullable = Match(TokenType.NULL);
            column.IsIdentity = Match(TokenType.IDENTITY);

            return column;
        }

        #endregion
    }
}