using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record BoxColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static BoxColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Size"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props.GetValueOrDefault("IsTrigger"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new BoxColliderInfo(Id,
				substitutedListeners,
				Size.SubstituteParameters(parameters, allValues),
				IsTrigger.SubstituteParameters(parameters, allValues));
	}
}