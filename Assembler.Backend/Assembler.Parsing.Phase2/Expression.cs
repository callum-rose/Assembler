namespace Assembler.Parsing2;

public record Expression(string Id, IReadOnlyList<string> ArgumentTypes, string ReturnType, string ExpressionBody);