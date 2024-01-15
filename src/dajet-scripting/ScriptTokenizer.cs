using System.Text;

namespace DaJet.Scripting
{
    public struct ScriptTokenizer : IDisposable
    {
        private StringReader _reader = null!;
        private readonly StringBuilder _lexeme = new(256);
        private readonly List<ScriptToken> _tokens = new();

        private int _line = 1;
        private int _start = 0;
        private int _position = 0;
        private char _char = char.MinValue;
        public ScriptTokenizer() { }
        public void Dispose()
        {
            _reader?.Dispose();
            _reader = null;

            _lexeme.Clear();
            _tokens.Clear();
            
            _line = 1;
            _start = 0;
            _position = 0;
            _char = char.MinValue;
    }
        public bool TryTokenize(in string script, out List<ScriptToken> tokens, out string error)
        {
            error = string.Empty;
            
            try
            {
                using (_reader = new StringReader(script))
                {
                    Tokenize();
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            tokens = new List<ScriptToken>(_tokens);

            Dispose();

            return string.IsNullOrWhiteSpace(error);
        }
        private string GetErrorText(string reason)
        {
            return $"{reason}. [{_char}] {{{_line}:{_position}}}";
        }

        private void Tokenize()
        {
            while (Consume())
            {
                if (_char == '\n')
                {
                    _line++;
                }
                else if (_char == ' ' || _char == '\r' || _char == '\t')
                {
                    // ignore
                }
                else if (_char == '+')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.Plus);
                }
                else if (_char == '-')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('-'))
                    {
                        SingleLineComment();
                    }
                    else
                    {
                        AddToken(TokenType.Minus);
                    }
                }
                else if (_char == '*') // Multiply | SELECT *
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.Star);
                }
                else if (_char == '/')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('*'))
                    {
                        MultiLineComment();
                    }
                    else
                    {
                        AddToken(TokenType.Divide);
                    }
                }
                else if (_char == '%')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.Modulo);
                }
                else if (_char == '=')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.Equals);
                }
                else if (_char == '!')
                {
                    _start = _position;
                    _lexeme.Append(_char);

                    if (Consume('='))
                    {
                        _lexeme.Append('=');
                        AddToken(TokenType.NotEquals);
                    }
                    else
                    {
                        _lexeme.Clear();
                    }
                }
                else if (_char == '>')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('='))
                    {
                        _lexeme.Append('=');
                        AddToken(TokenType.GreaterOrEquals);
                    }
                    else
                    {
                        AddToken(TokenType.Greater);
                    }
                }
                else if (_char == '<')
                {
                    _start = _position;

                    _lexeme.Append(_char);

                    if (Consume('='))
                    {
                        _lexeme.Append('=');
                        AddToken(TokenType.LessOrEquals);
                    }
                    else if (Consume('>'))
                    {
                        _lexeme.Append('>');
                        AddToken(TokenType.NotEquals);
                    }
                    else
                    {
                        AddToken(TokenType.Less);
                    }
                }
                else if (_char == ',')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.Comma);
                }
                else if (_char == ';')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.EndOfStatement);
                }
                else if (_char == '[')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.OpenSquareBracket);
                }
                else if (_char == ']')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.CloseSquareBracket);
                }
                else if (_char == '(')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.OpenRoundBracket);
                }
                else if (_char == ')')
                {
                    _start = _position;
                    _lexeme.Append(_char);
                    AddToken(TokenType.CloseRoundBracket);
                }
                else if (_char == '{')
                {
                    Entity();
                }
                else if (_char == '\'')
                {
                    SingleQuotedString();
                }
                else if (_char == '"')
                {
                    DoubleQuotedString();
                }
                else if (ParserHelper.IsNumeric(_char))
                {
                    if (_char == '0' && CheckNext('x'))
                    {
                        Binary();
                    }
                    else
                    {
                        Number();
                    }
                }
                else if (_char == '@')
                {
                    Variable();
                }
                else if (ParserHelper.IsAlphaNumeric(_char))
                {
                    Identifier();
                }
                else
                {
                    throw new Exception(GetErrorText("Unexpected character"));
                }
            }
        }
        private bool Consume()
        {
            int consumed = _reader.Read();

            if (consumed == -1)
            {
                return false;
            }

            _position++;

            _char = (char)consumed;
            
            return true;
        }
        private bool Consume(char expected)
        {
            char next = PeekNext();

            if (next == char.MinValue)
            {
                return false;
            }

            if (next == expected)
            {
                return Consume();
            }

            return false;
        }
        private char PeekNext()
        {
            int next = _reader.Peek();

            if (next == -1)
            {
                return char.MinValue;
            }

            return (char)next;
        }
        private bool CheckNext(char expected)
        {
            return (PeekNext() == expected);
        }
        private void AddToken(TokenType token)
        {
            _tokens.Add(new ScriptToken(token)
            {
                Line = _line,
                Offset = _start,
                Length = _position - _start + 1,
                Lexeme = _lexeme.ToString()
            });

            _lexeme.Clear();
        }
        
        private void SingleLineComment()
        {
            //_start = _position;
            _lexeme.Append(_char);

            while (PeekNext() != '\n' && Consume())
            {
                // read comment to the end of line
                _lexeme.Append(_char);
            }

            AddToken(TokenType.Comment);
        }
        private void MultiLineComment()
        {
            //_start = _position;
            _lexeme.Append(_char);

            while (PeekNext() != '*' && Consume())
            {
                // read comment until * is met

                if (_char == '\n')
                {
                    // process new line
                    _line++;
                }
                else
                {
                    _lexeme.Append(_char);
                }
            }

            // the * is met
            _ = Consume();
            _lexeme.Append(_char);

            if (_char != '*') // end of script
            {
                throw new Exception(GetErrorText("Unterminated comment"));
            }

            if (!Consume('/'))
            {
                throw new Exception(GetErrorText("Unexpected character"));
            }
            else
            {
                _lexeme.Append(_char);
            }

            AddToken(TokenType.Comment);
        }
        private void SingleQuotedString()
        {
            _start = _position;
            //_lexeme.Append(_char); do not include quotation mark

            while (PeekNext() != '\'' && Consume())
            {
                // read string literal until ' is met

                if (_char == '\n')
                {
                    // process new line
                    _line++;
                }
                else
                {
                    _lexeme.Append(_char);
                }
            }

            // the ' is met
            if (Consume())
            {
                //_lexeme.Append(_char); do not include quotation mark
            }

            if (_char != '\'') // end of script
            {
                throw new Exception(GetErrorText("Unterminated string"));
            }

            AddToken(TokenType.String);
        }
        private void DoubleQuotedString()
        {
            _start = _position;
            //_lexeme.Append(_char); do not include quotation mark

            while (PeekNext() != '\"' && Consume())
            {
                // read string literal until " is met

                if (_char == '\n')
                {
                    // process new line
                    _line++;
                }
                else
                {
                    _lexeme.Append(_char);
                }
            }

            // the " is met
            if (Consume())
            {
                //_lexeme.Append(_char); do not include quotation mark
            }

            if (_char != '\"') // end of script
            {
                throw new Exception(GetErrorText("Unterminated string"));
            }

            AddToken(TokenType.String);
        }
        private void Number()
        {
            _start = _position;
            _lexeme.Append(_char);

            while (ParserHelper.IsNumeric(PeekNext()) && Consume())
            {
                // read number literal
                _lexeme.Append(_char);
            }

            if (Consume('.'))
            {
                if (!ParserHelper.IsNumeric(PeekNext()))
                {
                    throw new Exception(GetErrorText("Unexpected character"));
                }

                _lexeme.Append(_char);

                while (ParserHelper.IsNumeric(PeekNext()) && Consume())
                {
                    // consume digits - fractional part
                    _lexeme.Append(_char);
                }
            }

            AddToken(TokenType.Number);
        }
        private void Binary()
        {
            _start = _position;
            _lexeme.Append(_char);

            if (!Consume('x'))
            {
                throw new Exception(GetErrorText("Unexpected character"));
            }

            _lexeme.Append(_char);

            while (ParserHelper.IsHexadecimal(PeekNext()) && Consume())
            {
                // read hex literal
                _lexeme.Append(_char);
            }

            AddToken(TokenType.Binary);
        }

        private void Entity()
        {
            _start = _position;
            _lexeme.Append(_char);

            while (PeekNext() != '}' && Consume())
            {
                // read Entity literal until } is met

                _lexeme.Append(_char);
            }

            // the } is met
            if (Consume())
            {
                _lexeme.Append(_char);
            }

            if (_char != '}') // end of script
            {
                throw new Exception(GetErrorText("Unterminated Entity literal"));
            }

            AddToken(TokenType.Entity);
        }
        private void Identifier()
        {
            _start = _position;
            _lexeme.Append(_char);

            char next = PeekNext();

            while (ParserHelper.IsAlphaNumeric(next) && Consume())
            {
                // read identifier
                _lexeme.Append(_char);
                next = PeekNext();
            }

            string test = _lexeme.ToString();

            if (ParserHelper.IsNullLiteral(test))
            {
                AddToken(TokenType.NULL);
            }
            else if (ParserHelper.IsBooleanLiteral(test))
            {
                AddToken(TokenType.Boolean);
            }
            else if (ParserHelper.IsKeyword(test, out TokenType token))
            {
                AddToken(token);
            }
            else
            {
                AddToken(TokenType.Identifier);
            }
        }
        private void Variable()
        {
            _start = _position;
            _lexeme.Append(_char);

            if (Consume('@')) // double @@
            {
                _lexeme.Append(_char);
            }

            char next = PeekNext();

            while (ParserHelper.IsAlphaNumeric(next) && Consume())
            {
                // read identifier
                _lexeme.Append(_char);
                next = PeekNext();
            }

            AddToken(TokenType.Variable);
        }
    }
}