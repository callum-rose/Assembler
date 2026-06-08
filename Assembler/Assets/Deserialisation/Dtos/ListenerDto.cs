using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public record ListenerDto
	{
		public object? EntityId { get; init; }
		public string? BehaviourId { get; init; }
		public object? EntityTag { get; init; }
		public object? BehaviourTag { get; init; }
		public Dictionary<string, string>? Outputs { get; init; }
	}

	/// <summary>Marker listener produced by the <c>!gameover</c> tag; targets the implicit end-game behaviour.</summary>
	public sealed record GameOverListenerDto : ListenerDto;
}
