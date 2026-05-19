using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CubeGizmoInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static CubeGizmoInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Size"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props.GetValueOrDefault("IsWire"), parameters: p),
				Transformer.CreateValueSource<Color>(v, props.GetValueOrDefault("Colour"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CubeGizmoInfo(Id,
				substitutedListeners,
				Size.SubstituteParameters(parameters, allValues),
				IsWire.SubstituteParameters(parameters, allValues),
				Colour.SubstituteParameters(parameters, allValues));
	}
}