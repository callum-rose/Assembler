using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public sealed record ListenerDto
	{
		public object? EntityId  { get; init; }
		public string? BehaviourId { get; init; }
		public string? EntityTag { get; init; }
		public string? BehaviourTag { get; init; }
		public Dictionary<string, string>? Outputs { get; init; }
	}

}