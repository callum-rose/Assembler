using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SpawnerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> TemplateId,
		ValueSource<Vector3> Position,
		ValueSource<Vector3> Rotation,
		IReadOnlyDictionary<string, ValueSource<object>> Parameters) : BehaviourInfo(Id, Listeners)
	{
		public static SpawnerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("TemplateId")),
				Transformer.CreateValueSource<Vector3>(v, props?.GetValueOrDefault("Position")),
				Transformer.CreateValueSource<Vector3>(v, props?.GetValueOrDefault("Rotation")),
				ParseParameters(v, props));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SpawnerInfo(Id,
				substitutedListeners,
				TemplateId.SubstituteParameters(parameters, allValues),
				Position.SubstituteParameters(parameters, allValues),
				Rotation.SubstituteParameters(parameters, allValues),
				Parameters.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value.SubstituteParameters(parameters, allValues)));

		private static IReadOnlyDictionary<string, ValueSource<object>> ParseParameters(
			IReadOnlyList<ValueInfo> values,
			Dictionary<string, AssemblerValue>? props)
		{
			if (props is null || !props.TryGetValue("Parameters", out var raw) || raw is not DictValue dictValue)
			{
				return new Dictionary<string, ValueSource<object>>();
			}

			return dictValue.Value.ToDictionary(
				kvp => kvp.Key,
				kvp => Transformer.CreateValueSource<object>(values, kvp.Value));
		}
	}
}
