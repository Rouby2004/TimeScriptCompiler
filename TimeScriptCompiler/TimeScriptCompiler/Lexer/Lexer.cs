using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeScriptCompiler.Lexer
{
    public class LexicalException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public LexicalException(string message, int line, int column) : base($"{message} (Line {line}, Column {column})")
        {
            Line = line;
            Column = column;
        }
    }

    public class Lexer
    {
        private readonly string _source;
        private int _pos = 0;
        private int _line = 1;
        private int _col = 1;
        private readonly List<Token> _tokens = new List<Token>();

        // indentation stack: number of spaces per nested level
        private readonly Stack<int> _indentStack = new Stack<int>();

        // whether we've just emitted a NEWLINE and are at start of a new logical line
        private bool _atLineStart = true;

        // track indent char mode: null = not yet set, ' ' or '\t'
        private char? _indentChar = null;

        private static readonly Dictionary<string, TokenType> _keywords = new Dictionary<string, TokenType>(StringComparer.Ordinal)
        {
            {"start", TokenType.START},
            {"set", TokenType.SET},
            {"show", TokenType.SHOW},
            {"listen", TokenType.LISTEN},
            {"if", TokenType.IF},
            {"else", TokenType.ELSE},
            {"loop", TokenType.LOOP},
            {"tick", TokenType.TICK},
            {"end", TokenType.END},

            // types
            {"hour", TokenType.HOUR},
            {"minute", TokenType.MINUTE},
            {"text", TokenType.TEXT},
            {"flag", TokenType.FLAG},

            // booleans
            {"True", TokenType.BOOLEAN},
            {"False", TokenType.BOOLEAN}
        };

        public Lexer(string source)
        {
            _source = source ?? string.Empty;
            // base indent level 0
            _indentStack.Push(0);
        }

        public List<Token> Tokenize()
        {
            _tokens.Clear();
            _pos = 0;
            _line = 1;
            _col = 1;
            _atLineStart = true;
            _indentChar = null;
            _indentStack.Clear();
            _indentStack.Push(0);

            while (!IsAtEnd())
            {
                if (_atLineStart)
                {
                    ProcessIndentation();
                    _atLineStart = false; // ProcessIndentation sets to false except in blank lines
                }

                char c = Peek();

                if (IsWhitespace(c))
                {
                    // spaces/tabs within line
                    Advance();
                    continue;
                }

                if (c == '#')
                {
                    ReadComment();
                    // After comment, the rest of the line is comment; next char is newline (or EOF).
                    continue;
                }

                if (c == '\n' || c == '\r')
                {
                    ReadNewline();
                    continue;
                }

                if (IsAlpha(c) || c == '_')
                {
                    ReadIdentifierOrKeyword();
                    continue;
                }

                if (IsDigit(c))
                {
                    ReadNumber();
                    continue;
                }

                if (c == '"')
                {
                    ReadString();
                    continue;
                }

                // operators & punctuation
                switch (c)
                {
                    case '+': AddTokenAndAdvance(TokenType.PLUS, "+"); break;
                    case '-': AddTokenAndAdvance(TokenType.MINUS, "-"); break;
                    case '*': AddTokenAndAdvance(TokenType.STAR, "*"); break;
                    case '/': AddTokenAndAdvance(TokenType.SLASH, "/"); break;
                    case '(': AddTokenAndAdvance(TokenType.LPAREN, "("); break;
                    case ')': AddTokenAndAdvance(TokenType.RPAREN, ")"); break;
                    case ',': AddTokenAndAdvance(TokenType.COMMA, ","); break;
                    case ':': AddTokenAndAdvance(TokenType.COLON, ":"); break;
                    case '=':
                        if (Match('='))
                            AddTokenAndAdvance(TokenType.ASSIGN, "=");
                        else
                            AddToken(TokenType.EQ, "==");
                        break;
                    case '!':
                        if (Match('='))
                            AddToken(TokenType.NEQ, "!=");
                        else
                            ThrowHere("Unexpected character '!'. Did you mean '!='?");
                        break;
                    case '<':
                        if (Match('='))
                            AddToken(TokenType.LE, "<=");
                        else
                            AddTokenAndAdvance(TokenType.LT, "<");
                        break;
                    case '>':
                        if (Match('='))
                            AddToken(TokenType.GE, ">=");
                        else
                            AddTokenAndAdvance(TokenType.GT, ">");
                        break;
                    default:
                        ThrowHere($"Unexpected character '{c}'");
                        break;
                }
            }

            // At end of input: if last token isn't NEWLINE, ensure we close indentation levels
            // Emit a final NEWLINE if last char wasn't newline to flush last logical line
            if (_tokens.Count == 0 || _tokens.Last().Type != TokenType.NEWLINE)
            {
                // simulate a newline to flush indentation
                _tokens.Add(new Token(TokenType.NEWLINE, "\\n", _line, _col));
            }

            // Close any remaining indentation levels
            while (_indentStack.Count > 1)
            {
                _indentStack.Pop();
                _tokens.Add(new Token(TokenType.DEDENT, "<DEDENT>", _line, _col));
            }

            _tokens.Add(new Token(TokenType.EOF, "<EOF>", _line, _col));
            return _tokens.ToList();
        }

        // ---------- Helpers ----------

        private void ProcessIndentation()
        {
            int startPos = _pos;
            int startCol = _col;

            // Count spaces/tabs at start of line until a non-space/tab or newline
            int count = 0;
            char? fstIndentChar = null;
            while (!IsAtEnd())
            {
                char c = Peek();
                if (c == ' ' || c == '\t')
                {
                    if (fstIndentChar == null) fstIndentChar = c;
                    // enforce indent char consistency across the whole file
                    if (_indentChar == null && fstIndentChar != null)
                        _indentChar = fstIndentChar;
                    if (_indentChar != null && fstIndentChar != null && fstIndentChar != _indentChar)
                        ThrowHere("Mixing tabs and spaces is not allowed for indentation.");

                    count += (c == '\t' ? 1 : 1); // treat both as 1 unit but we store actual char to enforce consistency
                    Advance();
                }
                else if (c == '\r' || c == '\n')
                {
                    // blank line -> emit NEWLINE and don't change indentation
                    ReadNewline();
                    // keep atLineStart true for next logical line
                    return;
                }
                else
                {
                    break;
                }
            }

            // Now we have count of indentation characters (each tab counts as one unit)
            int currentIndent = _indentStack.Peek();
            if (count > currentIndent)
            {
                _indentStack.Push(count);
                _tokens.Add(new Token(TokenType.INDENT, "<INDENT>", _line, startCol));
            }
            else if (count < currentIndent)
            {
                // dedent may pop multiple levels if needed
                while (_indentStack.Count > 0 && _indentStack.Peek() > count)
                {
                    _indentStack.Pop();
                    _tokens.Add(new Token(TokenType.DEDENT, "<DEDENT>", _line, startCol));
                }

                if (_indentStack.Peek() != count)
                    ThrowAt(_line, startCol, "Inconsistent dedent (unmatched indentation level).");
            }

            _atLineStart = false;
        }

        private void ReadComment()
        {
            int startCol = _col;
            var sb = new StringBuilder();
            while (!IsAtEnd() && Peek() != '\n' && Peek() != '\r')
            {
                sb.Append(Advance());
            }
            _tokens.Add(new Token(TokenType.COMMENT, sb.ToString(), _line, startCol));
        }

        private void ReadNewline()
        {
            // Support \r\n or \n
            if (Peek() == '\r')
            {
                Advance();
                if (!IsAtEnd() && Peek() == '\n') Advance();
            }
            else
            {
                Advance(); // '\n'
            }

            _tokens.Add(new Token(TokenType.NEWLINE, "\\n", _line, _col));
            _line++;
            _col = 1;
            _atLineStart = true;
        }

        private void ReadIdentifierOrKeyword()
        {
            int startCol = _col;
            var sb = new StringBuilder();
            while (!IsAtEnd() && (IsAlphaNumeric(Peek()) || Peek() == '_'))
            {
                sb.Append(Advance());
            }
            string lexeme = sb.ToString();

            // Keywords are case-sensitive in our design (types like "hour" lower-case).
            if (_keywords.TryGetValue(lexeme, out var kwType))
            {
                _tokens.Add(new Token(kwType, lexeme, _line, startCol));
            }
            else
            {
                _tokens.Add(new Token(TokenType.IDENTIFIER, lexeme, _line, startCol));
            }
        }

        private void ReadNumber()
        {
            int startCol = _col;
            var sb = new StringBuilder();
            bool seenDot = false;

            while (!IsAtEnd())
            {
                char c = Peek();
                if (IsDigit(c))
                {
                    sb.Append(Advance());
                }
                else if (c == '.' && !seenDot)
                {
                    seenDot = true;
                    sb.Append(Advance());
                    // allow a digit after dot; otherwise error will surface later (parser or numeric conversion)
                }
                else
                {
                    break;
                }
            }

            _tokens.Add(new Token(TokenType.NUMBER, sb.ToString(), _line, startCol));
        }

        private void ReadString()
        {
            int startCol = _col;
            Advance(); // consume the opening "

            var sb = new StringBuilder();
            bool closed = false;
            while (!IsAtEnd())
            {
                char c = Advance();
                if (c == '"')
                {
                    closed = true;
                    break;
                }
                if (c == '\\')
                {
                    if (IsAtEnd()) break;
                    char esc = Advance();
                    switch (esc)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        default:
                            // unknown escape -> include raw
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    if (c == '\n' || c == '\r')
                        ThrowAt(_line, _col, "Unterminated string literal (newline encountered inside string).");
                    sb.Append(c);
                }
            }

            if (!closed)
                ThrowAt(_line, _col, "Unterminated string literal (EOF reached).");

            _tokens.Add(new Token(TokenType.STRING, sb.ToString(), _line, startCol));
        }

        private void AddTokenAndAdvance(TokenType type, string lexeme)
        {
            AddToken(type, lexeme);
            Advance();
        }

        private void AddToken(TokenType type, string lexeme)
        {
            _tokens.Add(new Token(type, lexeme, _line, _col));
        }

        // basic char stream helpers
        private bool IsAtEnd() => _pos >= _source.Length;
        private char Peek() => IsAtEnd() ? '\0' : _source[_pos];
        private char PeekNext() => (_pos + 1 >= _source.Length) ? '\0' : _source[_pos + 1];

        private char Advance()
        {
            char c = _source[_pos++];
            if (c == '\n')
            {
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }
            return c;
        }

        private bool Match(char expected)
        {
            if (IsAtEnd()) return false;
            if (_source[_pos] != expected) return false;
            // consume
            _pos++;
            _col++;
            return true;
        }

        private static bool IsWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';
        private static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
        private static bool IsAlphaNumeric(char c) => char.IsLetterOrDigit(c) || c == '_';
        private static bool IsDigit(char c) => char.IsDigit(c);

        private void ThrowHere(string message)
        {
            throw new LexicalException(message, _line, _col);
        }

        private void ThrowAt(int line, int col, string message)
        {
            throw new LexicalException(message, line, col);
        }
    }
}
