using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TranslateInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Displacement)
		: BehaviourInfo(Id, Listeners)
	{
		public static TranslateInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Displacement"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TranslateInfo(Id,
				substitutedListeners,
				Displacement.Substitute(parameters, allValues));
	}
}