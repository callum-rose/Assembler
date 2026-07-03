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

		/// <summary>Where this listener was authored in the source YAML, threaded forward so a later
		/// reference-validation error can point at the offending line. <see cref="SourcePosition.None"/>
		/// when the converter couldn't capture it.</summary>
		public SourcePosition Position { get; init; } = SourcePosition.None;
	}

	/// <summary>Marker listener produced by the <c>!gameover</c> tag; targets the implicit end-game behaviour.</summary>
	public sealed record GameOverListenerDto : ListenerDto;
}
