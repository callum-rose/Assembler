using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SphereGizmoInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static SphereGizmoInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props.GetValueOrDefault("Radius"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props.GetValueOrDefault("IsWire"), parameters: p),
				Transformer.CreateValueSource<Color>(v, props.GetValueOrDefault("Colour"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SphereGizmoInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(parameters, allValues),
				IsWire.SubstituteParameters(parameters, allValues),
				Colour.SubstituteParameters(parameters, allValues));
	}
}