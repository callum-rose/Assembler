using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RigidbodyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<bool> UseGravity)
		: BehaviourInfo(Id, Listeners)
	{
		public static RigidbodyInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("UseGravity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new RigidbodyInfo(Id,
				substitutedListeners,
				UseGravity.Substitute(parameters, allValues));
	}
}