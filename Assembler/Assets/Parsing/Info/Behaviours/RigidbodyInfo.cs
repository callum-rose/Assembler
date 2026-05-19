using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RigidbodyInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> UseGravity)
		: BehaviourInfo(Id, Listeners)
	{
		public static RigidbodyInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(v, props?.GetValueOrDefault("UseGravity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new RigidbodyInfo(Id,
				substitutedListeners,
				UseGravity.SubstituteParameters(parameters, allValues));
	}
}