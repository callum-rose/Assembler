using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CubeGizmoInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static CubeGizmoInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Size"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsWire"), parameters: p),
				Transformer.Wrap<Color>(v, props?.GetValueOrDefault("Colour"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CubeGizmoInfo(Id,
				substitutedListeners,
				Size.Substitute(parameters, allValues),
				IsWire.Substitute(parameters, allValues),
				Colour.Substitute(parameters, allValues));
	}
}