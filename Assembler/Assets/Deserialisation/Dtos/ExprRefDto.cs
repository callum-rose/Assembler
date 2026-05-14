namespace Assembler.Deserialisation.Dtos
{
	public sealed record ExprRefDto
	{
		public string? ExpressionId { get; init; }
		public object[]? Arguments { get; init; }
	}
}