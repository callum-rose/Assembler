using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RotateTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static RotateTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new RotateTriggerInfo(Id, substitutedListeners);
	}
}