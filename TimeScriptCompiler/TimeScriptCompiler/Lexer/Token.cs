using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeScriptCompiler.Lexer
{
    public enum TokenType
    {
        // Structural
        INDENT,
        DEDENT,
        NEWLINE,
        EOF,

        // Keywords
        START, SET, SHOW, LISTEN, IF, ELSE, LOOP, TICK, END,

        // Types
        HOUR, MINUTE, TEXT, FLAG,

        // Literals & Ids
        IDENTIFIER,
        NUMBER,     // integer or float lexeme stored as string
        STRING,     // "..." (without quotes)
        BOOLEAN,    // True / False (could also be IDENTIFIER but we use BOOLEAN token)

        // Operators / punctuation
        PLUS, MINUS, STAR, SLASH,
        ASSIGN,         // =
        EQ, NEQ, LT, GT, LE, GE,
        LPAREN, RPAREN, COLON, COMMA,

        // Misc
        COMMENT,
        UNKNOWN
    }

    public sealed class Token
    {
        public TokenType Type { get; }
        public string Lexeme { get; }
        public int Line { get; }
        public int Column { get; }

        public Token(TokenType type, string lexeme, int line, int column)
        {
            Type = type;
            Lexeme = lexeme;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{Type}('{Lexeme}') @ {Line}:{Column}";
    }
}
