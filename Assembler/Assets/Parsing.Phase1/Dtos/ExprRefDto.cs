namespace Assembler.Parsing.Phase1.Dtos
{
	public sealed record ExprRefDto
	{
		public string? ExpressionId { get; init; }
		public object[]? Arguments { get; init; }
	}
}