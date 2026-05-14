namespace Assembler.Deserialisation.Dtos
{
	public sealed record ListenerDto
	{
		public object? EntityId  { get; init; }
		public string? BehaviourId { get; init; }
	}
	
	public sealed record AssetDto
	{
		public string? Id { get; init; }
		public string? Type { get; init; }
		public string? ResourcePath { get; init; }
	}
}