using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetPositionInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> ValueExpression)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetPositionInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Position"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SetPositionInfo(Id,
				substitutedListeners,
				ValueExpression.SubstituteParameters(parameters, allValues));
	}
}