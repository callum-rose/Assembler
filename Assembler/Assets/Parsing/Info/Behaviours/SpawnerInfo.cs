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
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("TemplateId")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Position")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Rotation")),
				ParseParameters(ctx, props));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SpawnerInfo(Id,
				substitutedListeners,
				TemplateId.SubstituteParameters(ctx),
				Position.SubstituteParameters(ctx),
				Rotation.SubstituteParameters(ctx),
				Parameters.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value.SubstituteParameters(ctx)));

		private static IReadOnlyDictionary<string, ValueSource<object>> ParseParameters(
			TransformContext ctx,
			IReadOnlyDictionary<string, AssemblerValue> props)
		{
			if (props is null || !props.TryGetValue("Parameters", out var raw) || raw is not DictValue dictValue)
			{
				return new Dictionary<string, ValueSource<object>>();
			}

			return dictValue.Value.ToDictionary(
				kvp => kvp.Key,
				kvp => ValueSourceFactory.CreateValueSource<object>(ctx, kvp.Value));
		}
	}
}
