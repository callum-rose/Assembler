using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SpawnerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<string> TemplateId,
		ValueSource<Vector3> Position) : BehaviourInfo(Id, Listeners)
	{
		public static SpawnerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("TemplateId")),
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Position")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SpawnerInfo(Id,
				substitutedListeners,
				TemplateId.Substitute(parameters, allValues),
				Position.Substitute(parameters, allValues));
	}
}