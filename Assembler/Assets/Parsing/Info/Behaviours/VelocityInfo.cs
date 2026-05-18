using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VelocityInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Velocity)
		: BehaviourInfo(Id, Listeners)
	{
		public static VelocityInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Velocity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VelocityInfo(Id,
				substitutedListeners,
				Velocity.Substitute(parameters, allValues));
	}
}