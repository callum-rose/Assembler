namespace Assembler.Parsing.Phase1.Dtos;

public class ExpressionDto
{
    public string? Id { get; set; }
    public string[]? ArgumentTypes { get; set; }
    public string[]? ArgumentNames { get; set; }
    public string? ReturnType { get; set; }
    public string? Expression { get; set; }
}
