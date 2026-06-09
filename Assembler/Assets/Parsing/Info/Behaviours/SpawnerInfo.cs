using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>One template the spawner may instantiate, with a selection weight (used in random mode).</summary>
	public sealed record SpawnTemplateInfo(string TemplateId, ValueSource<float> Weight)
	{
		public SpawnTemplateInfo SubstituteParameters(TransformContext ctx) =>
			this with { Weight = Weight.SubstituteParameters(ctx) };
	}

	public record SpawnerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> TemplateId,
		IReadOnlyList<SpawnTemplateInfo> Templates,
		ValueSource<string> Selection,
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
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("TemplateId")),
				ParseTemplates(ctx, props, id),
				Transformer.CreateOptionalValueSource<string>(ctx, props.GetValueOrDefault("Selection")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Position")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Rotation")),
				ParseParameters(ctx, props));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SpawnerInfo(Id,
				substitutedListeners,
				TemplateId.SubstituteParameters(ctx),
				Templates.Select(t => t.SubstituteParameters(ctx)).ToArray(),
				Selection.SubstituteParameters(ctx),
				Position.SubstituteParameters(ctx),
				Rotation.SubstituteParameters(ctx),
				Parameters.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value.SubstituteParameters(ctx)));

		private static IReadOnlyList<SpawnTemplateInfo> ParseTemplates(
			TransformContext ctx,
			IReadOnlyDictionary<string, AssemblerValue> props,
			string id)
		{
			var raw = props.GetValueOrDefault("Templates");
			if (raw is null or NoValue)
			{
				return System.Array.Empty<SpawnTemplateInfo>();
			}

			if (raw is not ListValue list)
			{
				throw new ParsingException(
					$"Spawner '{id}': Templates must be a list of template ids or {{ Template, Weight }} entries.");
			}

			return list.Value.Select(item => item switch
			{
				StringValue s => new SpawnTemplateInfo(s.Value, new ConstantSource<float>(1f)),
				DictValue d => new SpawnTemplateInfo(
					RequireTemplateId(d.Value.GetValueOrDefault("Template"), id),
					Transformer.CreateValueSource<float>(ctx, d.Value.GetValueOrDefault("Weight"), fallback: 1f)),
				_ => throw new ParsingException(
					$"Spawner '{id}': each Templates entry must be a template id or a {{ Template, Weight }} map.")
			}).ToArray();
		}

		private static string RequireTemplateId(AssemblerValue? value, string id) =>
			value is StringValue s
				? s.Value
				: throw new ParsingException($"Spawner '{id}': a Templates entry is missing its 'Template' id.");

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
				kvp => Transformer.CreateValueSource<object>(ctx, kvp.Value));
		}
	}
}
