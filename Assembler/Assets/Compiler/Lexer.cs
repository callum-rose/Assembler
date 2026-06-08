using System;
using System.Collections.Generic;
using System.Text;

namespace Assembler.Compiler.Compiler
{
	public class Lexer
	{
		private readonly string _code;
		private int _position;
		private int _line = 1;
		private int _column = 1;

		private static readonly Dictionary<string, TokenType> Keywords = new()
		{
			["if"] = TokenType.If,
			["else"] = TokenType.Else,
			["for"] = TokenType.For,
			["while"] = TokenType.While,
			["return"] = TokenType.Return,
			["break"] = TokenType.Break,
			["continue"] = TokenType.Continue,
			["var"] = TokenType.Var,
			["int"] = TokenType.Int,
			["string"] = TokenType.String_,
			["bool"] = TokenType.Bool,
			["float"] = TokenType.Float,
			["double"] = TokenType.Double,
			["void"] = TokenType.Void,
			["new"] = TokenType.New,
			["true"] = TokenType.BooleanLiteral,
			["false"] = TokenType.BooleanLiteral
		};

		public Lexer(string code)
		{
			_code = code;
		}

		public List<Token> Tokenize()
		{
			var tokens = new List<Token>();

			while (_position < _code.Length)
			{
				SkipWhitespace();

				if (_position >= _code.Length)
				{
					break;
				}

				var token = NextToken();

				if (token != null)
				{
					tokens.Add(token);
				}
			}

			tokens.Add(new Token(TokenType.EndOfFile, "", _line, _column));
			return tokens;
		}

		private Token? NextToken()
		{
			var c = _code[_position];

			if (char.IsDigit(c))
			{
				return ReadNumber();
			}

			if (char.IsLetter(c) || c == '_')
			{
				return ReadIdentifier();
			}

			if (c == '"')
			{
				return ReadString();
			}

			// Two-character operators and comments
			if (_position + 1 < _code.Length)
			{
				var twoChar = _code.Substring(_position, 2);

				// Handle single-line comments
				if (twoChar == "//")
				{
					SkipComment();
					return null;// Return null to skip this token
				}

				var twoCharToken = twoChar switch
				{
					"==" => new Token(TokenType.Equal, twoChar, _line, _column),
					"!=" => new Token(TokenType.NotEqual, twoChar, _line, _column),
					"<=" => new Token(TokenType.LessThanOrEqual, twoChar, _line, _column),
					">=" => new Token(TokenType.GreaterThanOrEqual, twoChar, _line, _column),
					"&&" => new Token(TokenType.And, twoChar, _line, _column),
					"||" => new Token(TokenType.Or, twoChar, _line, _column),
					"=>" => new Token(TokenType.Arrow, twoChar, _line, _column),
					"+=" => new Token(TokenType.PlusAssign, twoChar, _line, _column),
					"-=" => new Token(TokenType.MinusAssign, twoChar, _line, _column),
					"*=" => new Token(TokenType.MultiplyAssign, twoChar, _line, _column),
					"/=" => new Token(TokenType.DivideAssign, twoChar, _line, _column),
					"++" => new Token(TokenType.Increment, twoChar, _line, _column),
					"--" => new Token(TokenType.Decrement, twoChar, _line, _column),
					_ => null
				};

				if (twoCharToken != null)
				{
					_position += 2;
					_column += 2;
					return twoCharToken;
				}
			}

			// Rest remains the same...
			var token = c switch
			{
				'+' => new Token(TokenType.Plus, "+", _line, _column),
				'-' => new Token(TokenType.Minus, "-", _line, _column),
				'*' => new Token(TokenType.Multiply, "*", _line, _column),
				'/' => new Token(TokenType.Divide, "/", _line, _column),
				'%' => new Token(TokenType.Modulo, "%", _line, _column),
				'^' => new Token(TokenType.Xor, "^", _line, _column),
				'<' => new Token(TokenType.LessThan, "<", _line, _column),
				'>' => new Token(TokenType.GreaterThan, ">", _line, _column),
				'!' => new Token(TokenType.Not, "!", _line, _column),
				'=' => new Token(TokenType.Assign, "=", _line, _column),
				';' => new Token(TokenType.Semicolon, ";", _line, _column),
				',' => new Token(TokenType.Comma, ",", _line, _column),
				'.' => new Token(TokenType.Dot, ".", _line, _column),
				'(' => new Token(TokenType.LeftParen, "(", _line, _column),
				')' => new Token(TokenType.RightParen, ")", _line, _column),
				'{' => new Token(TokenType.LeftBrace, "{", _line, _column),
				'}' => new Token(TokenType.RightBrace, "}", _line, _column),
				'[' => new Token(TokenType.LeftBracket, "[", _line, _column),
				']' => new Token(TokenType.RightBracket, "]", _line, _column),
				'?' => new Token(TokenType.Question, "?", _line, _column),
				':' => new Token(TokenType.Colon, ":", _line, _column),
				_ => throw new CompileException($"Unexpected character '{c}'", _line, _column)
			};

			_position++;
			_column++;
			return token;
		}

		private Token ReadNumber()
		{
			var startColumn = _column;
			var sb = new StringBuilder();
			var dotCount = 0;

			while (_position < _code.Length && (char.IsDigit(_code[_position]) || _code[_position] == '.'))
			{
				if (_code[_position] == '.' && ++dotCount > 1)
				{
					// A second '.' means a malformed number like "1.2.3" — catch it here rather than
					// letting it lex as one token and blow up opaquely later at double.Parse.
					throw new CompileException($"Malformed number literal '{sb}.'", _line, startColumn);
				}

				sb.Append(_code[_position]);
				_position++;
				_column++;
			}

			return new Token(TokenType.Number, sb.ToString(), _line, startColumn);
		}

		private Token ReadIdentifier()
		{
			var startColumn = _column;
			var sb = new StringBuilder();

			while (_position < _code.Length && (char.IsLetterOrDigit(_code[_position]) || _code[_position] == '_'))
			{
				sb.Append(_code[_position]);
				_position++;
				_column++;
			}

			var value = sb.ToString();
			var type = Keywords.ContainsKey(value) ? Keywords[value] : TokenType.Identifier;

			return new Token(type, value, _line, startColumn);
		}

		private Token ReadString()
		{
			var startColumn = _column;
			var startLine = _line;
			var sb = new StringBuilder();

			_position++;// Skip opening quote
			_column++;

			while (_position < _code.Length && _code[_position] != '"')
			{
				var c = _code[_position];

				if (c == '\n')
				{
					// A newline before the closing quote means the literal was never terminated.
					throw new CompileException("Unterminated string literal", startLine, startColumn);
				}

				if (c == '\\')
				{
					if (_position + 1 >= _code.Length)
					{
						throw new CompileException("Unterminated string literal", startLine, startColumn);
					}

					_position++;
					_column++;
					sb.Append(TranslateEscape(_code[_position], startLine, _column));
				}
				else
				{
					sb.Append(c);
				}

				_position++;
				_column++;
			}

			if (_position >= _code.Length)
			{
				// Ran off the end of the source without ever seeing a closing quote.
				throw new CompileException("Unterminated string literal", startLine, startColumn);
			}

			_position++;// Skip closing quote
			_column++;

			return new Token(TokenType.String, sb.ToString(), startLine, startColumn);
		}

		// Translates the character following a backslash into the character it escapes, so that e.g.
		// "\n" in source becomes a real newline in the string value rather than a literal 'n'.
		private char TranslateEscape(char escaped, int line, int column)
		{
			return escaped switch
			{
				'n' => '\n',
				't' => '\t',
				'r' => '\r',
				'0' => '\0',
				'b' => '\b',
				'f' => '\f',
				'v' => '\v',
				'\\' => '\\',
				'"' => '"',
				'\'' => '\'',
				_ => throw new CompileException($"Unrecognised escape sequence '\\{escaped}'", line, column)
			};
		}

		private void SkipWhitespace()
		{
			while (_position < _code.Length && char.IsWhiteSpace(_code[_position]))
			{
				if (_code[_position] == '\n')
				{
					_line++;
					_column = 1;
				}
				else
				{
					_column++;
				}

				_position++;
			}
		}

		private void SkipComment()
		{
			// Skip the //
			_position += 2;
			_column += 2;

			// Skip until end of line or end of code
			while (_position < _code.Length && _code[_position] != '\n')
			{
				_position++;
				_column++;
			}
		}
	}
}
