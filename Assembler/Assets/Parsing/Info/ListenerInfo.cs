using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record ListenerInfo
	{
		public IReadOnlyDictionary<string, string> OutputMapping { get; init; } = new Dictionary<string, string>();
	}

	public sealed record DirectListenerInfo(BehaviourDescriptor BehaviourDescriptor) : ListenerInfo;

	public sealed record EntityTaggedListenerInfo(
		ValueSource<string> EntityTag,
		string BehaviourId) : ListenerInfo;

	public sealed record BehaviourTaggedListenerInfo(
		ValueSource<string> BehaviourTag) : ListenerInfo;
}
