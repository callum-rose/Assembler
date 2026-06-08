namespace Assembler.Deserialisation.Dtos
{
	public sealed record ExpressionDto
	{
		public string[]? ArgumentTypes { get; init; }
		public string[]? ArgumentNames { get; init; }
		public string? ReturnType { get; init; }
		public string[]? RegisterTypes { get; init; }
		public string[]? RegisterTypeStatics { get; init; }
		public string? Expression { get; init; }
		public string? CallableAs { get; init; }
	}
}
