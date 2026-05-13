namespace Assembler.Parsing.Phase1.Dtos
{
	public sealed record ListenerDto
	{
		public object? EntityId  { get; init; }
		public string? BehaviourId { get; init; }
	}
}