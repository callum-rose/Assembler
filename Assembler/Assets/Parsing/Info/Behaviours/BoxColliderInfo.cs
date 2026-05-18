using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record BoxColliderInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static BoxColliderInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Size"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new BoxColliderInfo(Id,
				substitutedListeners,
				Size.Substitute(parameters, allValues),
				IsTrigger.Substitute(parameters, allValues));
	}
}