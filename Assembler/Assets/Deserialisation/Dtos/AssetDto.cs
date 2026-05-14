namespace Assembler.Deserialisation.Dtos
{
	public sealed record AssetDto
	{
		public string? Id { get; init; }
		public string? Type { get; init; }
		public string? Source { get; init; }
		public string? Path { get; init; }
	}
}