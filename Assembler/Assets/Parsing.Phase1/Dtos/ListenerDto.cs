namespace Assembler.Parsing.Phase1.Dtos
{
	public sealed record ListenerDto
	{
		public string? EntityId  { get; init; }
		public string? BehaviourId { get; init; }
	}
}