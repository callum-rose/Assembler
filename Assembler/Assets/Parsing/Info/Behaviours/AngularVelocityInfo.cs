using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AngularVelocityInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> AngularVelocity)
		: BehaviourInfo(Id, Listeners)
	{
		public static AngularVelocityInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("AngularVelocity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new AngularVelocityInfo(Id,
				substitutedListeners,
				AngularVelocity.SubstituteParameters(parameters, allValues));
	}
}
