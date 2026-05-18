using System.Collections.Generic;

namespace Assembler.Parsing.Info
{

	public record ListenerInfo(BehaviourDescriptor BehaviourDescriptor)
	{
		public IReadOnlyDictionary<string, string> OutputMapping { get; init; } = new Dictionary<string, string>();
	}
}