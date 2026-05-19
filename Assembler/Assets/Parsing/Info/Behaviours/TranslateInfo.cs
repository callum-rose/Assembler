using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TranslateInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Displacement)
		: BehaviourInfo(Id, Listeners)
	{
		public static TranslateInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Displacement"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TranslateInfo(Id,
				substitutedListeners,
				Displacement.SubstituteParameters(parameters, allValues));
	}
}