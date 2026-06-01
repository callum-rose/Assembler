namespace Assembler.Deserialisation.Dtos
{
	public sealed record TextRefDto
	{
		public string? Key { get; init; }
		public object[]? Arguments { get; init; }
	}
}
