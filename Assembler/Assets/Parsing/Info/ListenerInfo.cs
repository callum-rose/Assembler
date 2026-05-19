using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public record ListenerInfo(BehaviourDescriptor BehaviourDescriptor)
	{
		public IReadOnlyDictionary<string, string> OutputMapping { get; init; } = new Dictionary<string, string>();
		public string? EntityTag { get; init; }
		public string? BehaviourTag { get; init; }

		public bool IsDynamic => EntityTag != null || BehaviourTag != null;
	}
}