using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public sealed record ListenerDto
	{
		public object? EntityId  { get; init; }
		public string? BehaviourId { get; init; }
		public object? EntityTag { get; init; }
		public object? BehaviourTag { get; init; }
		public Dictionary<string, string>? Outputs { get; init; }
	}

}
