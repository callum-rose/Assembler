using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record LineGizmoInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Start,
		ValueSource<Vector3> End,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static LineGizmoInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Start"), parameters: p),
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("End"), parameters: p),
				Transformer.CreateValueSource<Color>(v, props.GetValueOrDefault("Colour"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new LineGizmoInfo(Id,
				substitutedListeners,
				Start.SubstituteParameters(parameters, allValues),
				End.SubstituteParameters(parameters, allValues),
				Colour.SubstituteParameters(parameters, allValues));
	}
}
