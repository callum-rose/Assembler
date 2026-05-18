using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SphereColliderInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static SphereColliderInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Radius"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SphereColliderInfo(Id,
				substitutedListeners,
				Radius.Substitute(parameters, allValues),
				IsTrigger.Substitute(parameters, allValues));
	}
}