using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetPositionInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> ValueExpression)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetPositionInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Position"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SetPositionInfo(Id,
				substitutedListeners,
				ValueExpression.Substitute(parameters, allValues));
	}
}