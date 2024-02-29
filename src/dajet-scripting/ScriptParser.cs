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

                    if (node is not null)
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
                if (Match(TokenType.Boolean, TokenType.Number, TokenType.String, TokenType.Binary, TokenType.Entity))
                {
                    declare.Initializer = scalar();
                }
                else if (Check(TokenType.SELECT))
                {
                    declare.Initializer = select();
                }
                else
                {
                    throw new FormatException("Variable initializer expression expected.");
                }
            }

            if (Match(TokenType.EndOfStatement)) { /* IGNORE */ }

            Skip(TokenType.Comment);

            return declare;
        }
        private SyntaxNode use()
        {
            if (!Match(TokenType.String))
            {
                throw new FormatException("[USE] uri expected");
            }

            return new UseStatement()
            {
                Uri = Previous().Lexeme
            };
        }
        private SyntaxNode for_each()
        {
            ForEachStatement statement = new();

            if (!Match(TokenType.EACH))
            {
                throw new FormatException("[FOR] EACH keyword expected");
            }

            if (!Match(TokenType.Variable) || variable() is not VariableReference _variable)
            {
                throw new FormatException("[FOR] variable identifier expected");
            }

            statement.Variable = _variable;

            if (!Match(TokenType.IN))
            {
                throw new FormatException("[FOR] IN keyword expected");
            }

            if (!Match(TokenType.Variable) || variable() is not VariableReference _iterator)
            {
                throw new FormatException("[FOR] iterator identifier expected");
            }

            statement.Iterator = _iterator;

            Skip(TokenType.Comment);

            if (Match(TokenType.MAXDOP)) // optional
            {
                int minus = Match(TokenType.Minus) ? -1 : 1;

                if (!Match(TokenType.Number) || scalar() is not ScalarExpression _scalar)
                {
                    throw new FormatException("[FOR] MAXDOP parameter expected");
                }

                statement.DegreeOfParallelism = minus * int.Parse(_scalar.Literal);

                Skip(TokenType.Comment);
            }

            return statement;
        }
        private SyntaxNode statement()
        {
            if (Match(TokenType.Comment)) { return comment(); }
            else if (Match(TokenType.DECLARE)) { return declare(); }
            else if (Match(TokenType.USE)) { return use(); }
            else if (Match(TokenType.FOR)) { return for_each(); }
            else if (Match(TokenType.CREATE)) { return create_statement(); }
            else if (Check(TokenType.SELECT)) { return select_statement(); }
            else if (Check(TokenType.INSERT)) { return insert_statement(); }
            else if (Check(TokenType.UPDATE)) { return update_statement(); }
            else if (Check(TokenType.DELETE)) { return delete_statement(); }
            else if (Check(TokenType.UPSERT)) { return upsert_statement(); }
            else if (Check(TokenType.STREAM)) { return stream_statement(); }
            else if (Check(TokenType.CONSUME)) { return consume_statement(); }
            else if (Check(TokenType.PRODUCE)) { return produce_statement(); }
            else if (Check(TokenType.IMPORT)) { return import_statement(); }
            else if (Match(TokenType.DROP)) { return drop_statement(); }
            else if (Match(TokenType.EndOfStatement)) { return null; }

            Ignore();

            throw new FormatException($"Unknown statement: {Previous()}");
        }
        private SyntaxNode create_statement()
        {
            if (Match(TokenType.TYPE))
            {
                return create_type();
            }

            if (Match(TokenType.SEQUENCE))
            {
                return create_sequence();
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
        private SyntaxNode drop_statement()
        {
            if (Match(TokenType.SEQUENCE))
            {
                return drop_sequence();
            }

            throw new FormatException("Unknown DROP statement.");
        }

        #region "CREATE TABLE STATEMENT"
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

            bool expect_close = Match(TokenType.OpenRoundBracket);

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

            bool expect_close = Match(TokenType.OpenRoundBracket);

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
                    Expression = union(),
                    CommonTables = root
                };
            }
            else if (Check(TokenType.STREAM))
            {
                SelectStatement select = stream_statement();
                select.CommonTables = root;
                return select;
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

            if (Check(TokenType.INSERT)) { cte.Expression = insert_statement(); }
            else if (Check(TokenType.UPDATE)) { cte.Expression = update_statement(); }
            else if (Check(TokenType.DELETE)) { cte.Expression = delete_statement(); }
            else
            {
                cte.Expression = union();
            }

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
        private SyntaxNode select_statement() { return new SelectStatement() { Expression = union() }; }
        private SelectStatement stream_statement()
        {
            if (Current().Type == TokenType.STREAM)
            {
                Current().Override(TokenType.SELECT);
            }

            SelectStatement select = new()
            {
                IsStream = true,
                Expression = union()
            };

            ValidateStreamStatement(in select);

            return select;
        }
        private void ValidateStreamStatement(in SelectStatement node)
        {
            if (node.Expression is SelectExpression select)
            {
                ValidateStreamStatement(in select);
            }
            else if (node.Expression is TableUnionOperator union)
            {
                ValidateStreamStatement(in union);
            }
        }
        private void ValidateStreamStatement(in SelectExpression node)
        {
            if (node.Into is null)
            {
                throw new FormatException("[STREAM] INTO clause expected");
            }
            else if (node.Into.Value is null)
            {
                throw new FormatException("[STREAM] INTO variable expected");
            }
        }
        private void ValidateStreamStatement(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select)
            {
                ValidateStreamStatement(in select);
            }
        }
        ///<returns>SelectExpression or TableUnionOperator</returns>
        private SyntaxNode union()
        {
            SyntaxNode node = union_operator();

            if (node is not TableUnionOperator _union)
            {
                return node; // SelectExpression
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
        ///<returns>SelectExpression or TableUnionOperator</returns>
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
            if (Match(TokenType.INTO)) { select.Into = into_clause(select.Columns); }
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
        ///<returns>TableReference or TableExpression</returns>
        private SyntaxNode table()
        {
            if (Match(TokenType.Identifier, TokenType.Variable))
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

            table.Alias = alias(); //NOTE: the function can return an empty string alias

            return table;
        }
        ///<returns>TableReference or TableExpression or TableJoinOperator</returns>
        private SyntaxNode join()
        {
            SyntaxNode left = table();

            if (left is TableExpression expression)
            {
                disable_correlation_flag(in expression);
            }

            Skip(TokenType.Comment);

            while (Match(TokenType.LEFT, TokenType.RIGHT, TokenType.INNER,
                TokenType.FULL, TokenType.CROSS, TokenType.OUTER, TokenType.APPEND))
            {
                TokenType _operator = Previous().Type;

                Type memberType = typeof(Array);

                if (_operator == TokenType.APPEND)
                {
                    if (Match(TokenType.Identifier) &&
                        ParserHelper.IsDataType(Previous().Lexeme, out Type type) &&
                        (type == typeof(Array) || type == typeof(object)))
                    {
                        memberType = type; //THINK: make "array" & "object" keywords !?
                    }
                }
                else if (Match(TokenType.APPLY))
                {
                    if (_operator == TokenType.CROSS) { _operator = TokenType.CROSS_APPLY; }
                    else if (_operator == TokenType.OUTER) { _operator = TokenType.OUTER_APPLY; }
                    else { throw new FormatException("[APPLY] CROSS or OUTER keyword expected"); }
                }
                else if (!Match(TokenType.JOIN))
                {
                    throw new FormatException("JOIN keyword expected.");
                }

                bool parse_on_clause = false;

                SyntaxNode right = table();

                TableExpression subquery = right as TableExpression;

                if (subquery is not null && string.IsNullOrEmpty(subquery.Alias))
                {
                    throw new FormatException($"[{_operator}] Table expression alias expected.");
                }

                if (_operator == TokenType.CROSS_APPLY ||
                    _operator == TokenType.OUTER_APPLY ||
                    _operator == TokenType.APPEND)
                {
                    if (subquery is null)
                    {
                        throw new FormatException($"[{_operator}] Table expression expected.");
                    }
                }
                else if (_operator == TokenType.CROSS) //NOTE: CROSS JOIN operator does not use ON clause
                {
                    if (subquery is not null)
                    {
                        disable_correlation_flag(in subquery);
                    }
                }
                else if (Match(TokenType.ON)) // { LEFT | RIGHT | INNER | FULL } JOIN
                {
                    parse_on_clause = true;

                    if (subquery is not null)
                    {
                        disable_correlation_flag(in subquery);
                    }
                }
                else
                {
                    throw new FormatException("ON keyword expected.");
                }

                if (parse_on_clause)
                {
                    Skip(TokenType.Comment);
                }

                if (_operator == TokenType.APPEND)
                {
                    MemberAccessDescriptor descriptor = new()
                    {
                        Member = subquery.Alias,
                        MemberType = memberType
                    };
                    disable_correlation_flag(in subquery);
                    set_member_access_descriptor(in subquery, in descriptor);
                }

                left = new TableJoinOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right,
                    On = parse_on_clause ? on_clause() : null
                };
            }

            return left;
        }
        private void disable_correlation_flag(in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                select.IsCorrelated = false;
            }
            else if (node is TableExpression table)
            {
                disable_correlation_flag(in table);
            }
            else if (node is TableUnionOperator union)
            {
                disable_correlation_flag(in union);
            }
        }
        private void disable_correlation_flag(in TableExpression table)
        {
            disable_correlation_flag(table.Expression);
        }
        private void disable_correlation_flag(in TableUnionOperator union)
        {
            disable_correlation_flag(union.Expression1);
            disable_correlation_flag(union.Expression2);
        }
        private void set_member_access_descriptor(in SyntaxNode node, in MemberAccessDescriptor descriptor)
        {
            if (node is SelectExpression select)
            {
                select.Binding = descriptor;
            }
            else if (node is TableExpression table)
            {
                set_member_access_descriptor(in table, in descriptor);
            }
            else if (node is TableUnionOperator union)
            {
                set_member_access_descriptor(in union, in descriptor);
            }
        }
        private void set_member_access_descriptor(in TableExpression table, in MemberAccessDescriptor descriptor)
        {
            set_member_access_descriptor(table.Expression, in descriptor);
        }
        private void set_member_access_descriptor(in TableUnionOperator union, in MemberAccessDescriptor descriptor)
        {
            set_member_access_descriptor(union.Expression1, in descriptor);
            set_member_access_descriptor(union.Expression2, in descriptor);
        }
        private FromClause from_clause() { return new FromClause() { Expression = join() }; }
        private IntoClause into_clause(in List<ColumnExpression> columns)
        {
            IntoClause clause = new() { Columns = columns };

            if (Match(TokenType.Identifier))
            {
                clause.Table = new TableReference()
                {
                    Identifier = Previous().Lexeme
                };
                return clause;
            }
            else if (Match(TokenType.Variable))
            {
                clause.Value = new VariableReference()
                {
                    Identifier = Previous().Lexeme
                };
                return clause;
            }

            throw new FormatException("INTO: table or variable identifier expected.");
        }
        private string alias()
        {
            if (Match(TokenType.AS))
            {
                if (Match(TokenType.Identifier, TokenType.TYPE))
                {
                    return Previous().Lexeme;
                }
                else
                {
                    throw new FormatException("Alias expected.");
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

            SyntaxNode node = expression();

            if (node is ComparisonOperator assignment) //NOTE: Summa = SUM(t1.Value)
            {
                if (assignment.Token != TokenType.Equals)
                {
                    throw new FormatException("Column definition error: assignment expected");
                }

                if (assignment.Expression1 is not ColumnReference column) // left operand
                {
                    throw new FormatException("Column definition error: identifier expected");
                }

                return new ColumnExpression() //THINK: multi-part identifier assignment
                {
                    Alias = column.Identifier,          // left  operand (column name)
                    Expression = assignment.Expression2 // right operand (initializer)
                };
            }

            return new ColumnExpression() //NOTE: SUM(t1.Value) AS Summa
            {
                Expression = node, Alias = alias()
            };
        }
        private void select_clause(in SelectExpression select)
        {
            Skip(TokenType.Comment);
            select.Distinct = Match(TokenType.DISTINCT);
            Skip(TokenType.Comment);

            Skip(TokenType.Comment);
            select.Top = top_clause();
            Skip(TokenType.Comment);

            select.Columns.Add(column());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                select.Columns.Add(column());

                Skip(TokenType.Comment);
            }
        }
        private TopClause top_clause()
        {
            if (!Match(TokenType.TOP))
            {
                return null;
            }

            bool expect_close = Match(TokenType.OpenRoundBracket);

            TopClause clause = new() { Expression = expression() };

            if (expect_close && !Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("TOP clause: close round bracket expected.");
            }

            return clause;
        }
        private OnClause on_clause() { return new OnClause() { Expression = predicate() }; }
        private WhereClause where_clause() { return new WhereClause() { Expression = predicate() }; }
        private HavingClause having_clause() { return new HavingClause() { Expression = predicate() }; }
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

        #region "PREDICATE"
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
        #endregion

        #region "EXPRESSION"
        private SyntaxNode expression()
        {
            return comparison();
        }
        private SyntaxNode comparison()
        {
            SyntaxNode left = addition();

            Skip(TokenType.Comment);

            while (Match(TokenType.IS,
                TokenType.Equals, TokenType.NotEquals,
                TokenType.Greater, TokenType.GreaterOrEquals,
                TokenType.Less, TokenType.LessOrEquals,
                TokenType.NOT, TokenType.IN, TokenType.LIKE, TokenType.BETWEEN))
            {
                TokenType _operator = Previous().Type;

                bool negate = (_operator == TokenType.NOT);

                if (negate)
                {
                    if (Match(TokenType.IN, TokenType.LIKE, TokenType.BETWEEN))
                    {
                        _operator = Previous().Type;
                    }
                    else
                    {
                        throw new FormatException("IN, LIKE or BETWEEN keyword expected");
                    }
                }

                if (_operator == TokenType.IS)
                {
                    return new ComparisonOperator()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = is_right_operand()
                    };
                }
                else if (_operator == TokenType.IN)
                {
                    return new ComparisonOperator()
                    {
                        Token = _operator,
                        Modifier = negate ? TokenType.NOT : TokenType.Ignore,
                        Expression1 = left,
                        Expression2 = in_right_operand()
                    };
                }
                else if (_operator == TokenType.LIKE)
                {
                    ComparisonOperator expression = new()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = terminal()
                    };

                    if (!(expression.Expression2 is ScalarExpression
                        || expression.Expression2 is VariableReference))
                    {
                        throw new FormatException("[LIKE] string pattern or variable reference expected");
                    }

                    if (negate) { expression.Modifier = TokenType.NOT; }

                    return expression;
                }
                else if (_operator == TokenType.BETWEEN)
                {
                    ComparisonOperator expression = new()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = and()
                    };

                    if (expression.Expression2 is not BinaryOperator)
                    {
                        throw new FormatException("[BETWEEN] AND operator expected");
                    }

                    if (negate) { expression.Modifier = TokenType.NOT; }

                    return expression;
                }
                else if (Match(TokenType.ALL, TokenType.ANY))
                {
                    TokenType modifier = Previous().Type;

                    ComparisonOperator expression = new()
                    {
                        Token = _operator,
                        Modifier = modifier,
                        Expression1 = left,
                        Expression2 = table()
                    };

                    if (expression.Expression2 is not TableExpression)
                    {
                        throw new FormatException($"[{modifier}] table expression expected");
                    }

                    return expression;
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
        ///<returns>TableExpression or ValuesExpression</returns>
        private SyntaxNode in_right_operand()
        {
            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("[IN] Open round bracket expected.");
            }

            TokenType token = Current().Type;

            SyntaxNode expression = (token == TokenType.SELECT)
                ? new TableExpression() { Expression = union() }
                : new ValuesExpression() { Values = array_of_values() };

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("[IN] Close round bracket expected.");
            }

            return expression;
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

            return grouping();
        }
        ///<returns>GroupOperator (round brackets) or TableExpression without alias</returns>
        private SyntaxNode grouping()
        {
            if (Match(TokenType.OpenRoundBracket))
            {
                TokenType token = Current().Type;

                SyntaxNode expression = (token == TokenType.SELECT)
                    ? union() //NOTE: SelectExpression | TableUnionOperator
                    : predicate(); //NOTE: recursion

                if (!Match(TokenType.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket token expected.");
                }

                return (token == TokenType.SELECT)
                    ? new TableExpression() { Expression = expression }
                    : new GroupOperator() { Expression = expression };
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
            else if (Match(TokenType.Star))
            {
                return star();
            }
            else if (Match(TokenType.CASE))
            {
                return case_expression();
            }
            else if (Match(TokenType.EXISTS))
            {
                return exists_function();
            }
            else if (Match(TokenType.TYPE)) //NOTE: exceptional keyword - see also alias() function
            {
                return new ColumnReference() { Identifier = Previous().Lexeme };
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
        private SyntaxNode star()
        {
            return new StarExpression();
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
            else if (scalar.Token == TokenType.Number && !scalar.Literal.Contains('.'))
            {
                scalar.Token = TokenType.Integer;
            }

            return scalar;
        }
        private SyntaxNode variable()
        {
            string identifier = Previous().Lexeme;

            if (identifier.Contains('.')) //NOTE: multi-part identifier
            {
                return new MemberAccessExpression()
                {
                    Identifier = identifier
                };
            }
            else
            {
                return new VariableReference()
                {
                    Identifier = identifier
                };
            }
        }
        private SyntaxNode identifier()
        {
            string identifier = Previous().Lexeme;

            if (ParserHelper.IsFunction(identifier, out TokenType token))
            {
                return function(token, identifier); // database built-in function
            }
            else if (Check(TokenType.OpenRoundBracket))
            {
                return function(TokenType.UDF, identifier); // user-defined function
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
        private FunctionExpression exists_function()
        {
            SyntaxNode parameter = table();

            if (parameter is not TableExpression)
            {
                throw new FormatException("[EXISTS] table expression expected");
            }

            FunctionExpression function = new()
            {
                Name = "EXISTS",
                Token = TokenType.EXISTS
            };

            function.Parameters.Add(parameter);

            return function;
        }
        private List<SyntaxNode> array_of_values()
        {
            List<SyntaxNode> values = new()
            {
                terminal()
            };

            while (Match(TokenType.Comma))
            {
                values.Add(terminal());
            }

            return values;
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

            if (token == TokenType.COUNT && Match(TokenType.DISTINCT))
            {
                function.Modifier = TokenType.DISTINCT;
            }

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

            //THINK: implement function parameters validation
            if (function.Name.ToUpperInvariant() == "VECTOR")
            {
                if (function.Parameters is null)
                {
                    throw new FormatException("VECTOR function: missing parameter.");
                }
                else if (function.Parameters.Count == 0)
                {
                    throw new FormatException("VECTOR function: missing parameter.");
                }
                else if (function.Parameters.Count > 1)
                {
                    throw new FormatException("VECTOR function: too many parameters.");
                }

                if (function.Parameters[0] is not ScalarExpression scalar || scalar.Token != TokenType.String)
                {
                    throw new FormatException("VECTOR function: string parameter type expected.");
                }

                if (string.IsNullOrWhiteSpace(scalar.Literal))
                {
                    throw new FormatException("VECTOR function: parameter value must be non-empty string.");
                }
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

            if (Match(TokenType.OUTPUT))
            {
                update.Output = output_clause();
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

            if (Match(TokenType.OUTPUT))
            {
                delete.Output = output_clause();
            }

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

            Skip(TokenType.Comment);
            if (Match(TokenType.INTO)) { output.Into = into_clause(output.Columns); }
            Skip(TokenType.Comment);

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
            CreateTypeStatement statement = new();

            if (!Match(TokenType.Identifier))
            {
                throw new FormatException("Type identifier expected.");
            }

            statement.Identifier = Previous().Lexeme;

            if (!Match(TokenType.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            statement.Columns.Add(column_definition());

            while (Match(TokenType.Comma))
            {
                statement.Columns.Add(column_definition());
            }

            if (!Match(TokenType.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            return statement;
        }
        private ColumnDefinition column_definition()
        {
            if (!Match(TokenType.Identifier)) { throw new FormatException("Column identifier expected."); }

            ColumnDefinition column = new() { Name = Previous().Lexeme };

            if (!Match(TokenType.Identifier)) { throw new FormatException("Data type identifier expected."); }

            column.Type = type();

            return column;
        }
        #endregion

        #region "SEQUENCE"
        private SyntaxNode drop_sequence()
        {
            DropSequenceStatement statement = new();

            if (!Match(TokenType.Identifier)) { throw new FormatException("Sequence identifier expected."); }

            statement.Identifier = Previous().Lexeme;

            return statement;
        }
        private SyntaxNode create_sequence()
        {
            CreateSequenceStatement statement = new();

            if (!Match(TokenType.Identifier)) { throw new FormatException("Sequence identifier expected."); }

            statement.Identifier = Previous().Lexeme;

            if (Match(TokenType.AS))
            {
                if (!Match(TokenType.Identifier)) { throw new FormatException("Data type identifier expected."); }

                statement.DataType = type();
            }
            
            if (Match(TokenType.START))
            {
                if (!Match(TokenType.WITH)) { throw new FormatException("START WITH keyword expected."); }

                if (!Match(TokenType.Number)) { throw new FormatException("Integer literal expected."); }

                if (!int.TryParse(Previous().Lexeme, out int start))
                {
                    throw new FormatException("Integer literal expected.");
                }

                statement.StartWith = start;
            }
            
            if (Match(TokenType.INCREMENT))
            {
                if (!Match(TokenType.BY)) { throw new FormatException("INCREMENT BY keyword expected."); }

                if (!Match(TokenType.Number)) { throw new FormatException("Integer literal expected."); }

                if (!int.TryParse(Previous().Lexeme, out int increment))
                {
                    throw new FormatException("Integer literal expected.");
                }

                statement.Increment = increment;
            }

            if (Match(TokenType.CACHE))
            {
                if (!Match(TokenType.Number)) { throw new FormatException("Integer literal expected."); }

                if (!int.TryParse(Previous().Lexeme, out int cache))
                {
                    throw new FormatException("Integer literal expected.");
                }

                statement.CacheSize = cache;
            }

            return statement;
        }
        #endregion

        #region "CONSUME STATEMENT"
        private SyntaxNode consume_statement()
        {
            if (!Match(TokenType.CONSUME))
            {
                throw new FormatException("CONSUME keyword expected.");
            }

            ConsumeStatement consume = new();

            Skip(TokenType.Comment);

            if (Match(TokenType.String)) //NOTE: stream processor URI
            {
                return consume_stream_statement(in consume);
            }
            else
            {
                consume.Top = top_clause();
            }
            
            Skip(TokenType.Comment);

            if (consume.Top is null) { throw new FormatException("CONSUME: TOP keyword expected."); }

            if (Match(TokenType.WITH))
            {
                if (Match(TokenType.STRICT))
                {
                    consume.StrictOrderRequired = true;
                }
                else if (Match(TokenType.RANDOM))
                {
                    consume.StrictOrderRequired = false;
                }
                else
                {
                    throw new FormatException($"CONSUME: STRICT or RANDOM keyword expected.");
                }

                if (!Match(TokenType.ORDER)) { throw new FormatException($"CONSUME: (STRICT or RANDOM) ORDER keyword expected."); }
            }

            Skip(TokenType.Comment);
            select_columns(in consume);
            Skip(TokenType.Comment);
            if (Match(TokenType.INTO)) { consume.Into = into_clause(consume.Columns); }
            Skip(TokenType.Comment);
            if (Match(TokenType.FROM)) { consume.From = from_clause(); }
            Skip(TokenType.Comment);
            if (Match(TokenType.WHERE)) { consume.Where = where_clause(); }
            Skip(TokenType.Comment);
            if (Match(TokenType.ORDER)) { consume.Order = order_clause(); }
            Skip(TokenType.Comment);

            if (consume.From.Expression is TableReference table && table.Identifier.StartsWith('@'))
            {
                throw new FormatException($"CONSUME {table.Identifier}: table variable targeting is not allowed.");
            }

            return consume;
        }
        private SyntaxNode consume_stream_statement(in ConsumeStatement consume)
        {
            consume.Target = Previous().Lexeme;

            Skip(TokenType.Comment);

            if (Match(TokenType.WITH))
            {
                consume_options(in consume);
            }

            Skip(TokenType.Comment);
            
            if (!Match(TokenType.INTO))
            {
                throw new FormatException($"CONSUME: INTO keyword expected");
            }

            Skip(TokenType.Comment);

            consume.Into = into_clause(consume.Columns);

            if (consume.Into is null || consume.Into.Value is null)
            {
                throw new FormatException("CONSUME: INTO variable identifier expected");
            }

            Skip(TokenType.Comment);

            return consume;
        }
        private void select_columns(in ConsumeStatement consume)
        {
            consume.Columns.Add(column());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                consume.Columns.Add(column());

                Skip(TokenType.Comment);
            }
        }
        private void consume_options(in ConsumeStatement consume)
        {
            consume.Options.Add(column());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                consume.Options.Add(column());

                Skip(TokenType.Comment);
            }
        }
        #endregion

        #region "IMPORT STATEMENT"
        private SyntaxNode import_statement()
        {
            if (!Match(TokenType.IMPORT))
            {
                throw new FormatException("IMPORT keyword expected.");
            }

            ImportStatement statement = new();

            Skip(TokenType.Comment);

            if (!Match(TokenType.String))
            {
                throw new FormatException("IMPORT: data source URL expected.");
            }
            else
            {
                statement.Source = Previous().Lexeme;
            }

            Skip(TokenType.Comment);

            if (!Match(TokenType.INTO))
            {
                throw new FormatException("INTO keyword expected.");
            }

            Skip(TokenType.Comment);

            statement.Target = table_variables();
            
            Skip(TokenType.Comment);

            if (Match(TokenType.EndOfStatement)) { /* IGNORE */ }

            Skip(TokenType.Comment);

            return statement;
        }
        private List<VariableReference> table_variables()
        {
            if (!Match(TokenType.Variable)) { throw new FormatException("IMPORT: table variable expected."); }

            List<VariableReference> tables = new()
            {
                new VariableReference() { Identifier = Previous().Lexeme }
            };

            while (Match(TokenType.Comma))
            {
                if (!Match(TokenType.Variable)) { throw new FormatException("IMPORT: table variable expected."); }

                tables.Add(new VariableReference() { Identifier = Previous().Lexeme });
            }

            return tables;
        }
        #endregion

        #region "PRODUCE STATEMENT"
        private SyntaxNode produce_statement()
        {
            if (!Match(TokenType.PRODUCE))
            {
                throw new FormatException("PRODUCE keyword expected");
            }

            ProduceStatement produce = new();

            Skip(TokenType.Comment);

            if (!Match(TokenType.String))
            {
                throw new FormatException("PRODUCE: uri expected");
            }

            produce.Target = Previous().Lexeme;

            Skip(TokenType.Comment);

            if (Match(TokenType.WITH))
            {
                produce_options(in produce);
            }

            if (!Match(TokenType.SELECT))
            {
                throw new FormatException($"PRODUCE: SELECT keyword expected");
            }

            produce_columns(in produce);

            return produce;
        }
        private void produce_options(in ProduceStatement produce)
        {
            produce.Options.Add(column());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                produce.Options.Add(column());

                Skip(TokenType.Comment);
            }
        }
        private void produce_columns(in ProduceStatement produce)
        {
            produce.Columns.Add(column());

            Skip(TokenType.Comment);

            while (Match(TokenType.Comma))
            {
                Skip(TokenType.Comment);

                produce.Columns.Add(column());

                Skip(TokenType.Comment);
            }
        }
        #endregion
    }
}