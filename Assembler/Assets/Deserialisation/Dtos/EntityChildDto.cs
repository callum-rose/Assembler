namespace Assembler.Deserialisation.Dtos
{
	public sealed record EntityChildDto
	{
		public string? Ref { get; init; }
		public EntityDto? Entity { get; init; }
	}
}
