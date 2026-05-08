namespace Assembler.Compiler;

public class Token(TokenType type, string value, int line, int column)
{
	public TokenType Type { get; } = type;
	public string Value { get; } = value;
	public int Line { get; } = line;
	public int Column { get; } = column;
}