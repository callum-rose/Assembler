using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SphereColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static SphereColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("Radius"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SphereColliderInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(parameters, allValues),
				IsTrigger.SubstituteParameters(parameters, allValues));
	}
}