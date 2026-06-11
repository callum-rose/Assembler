using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation.Dtos;
using Assembler.Extensions;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	// Parses listener declarations into ListenerInfo: both the top-level `Listeners:` field on a
	// behaviour (GetListeners) and listeners authored inside a behaviour's properties as already-converted
	// AssemblerValues (ParseNestedListeners, e.g. a state machine's per-state OnEnter/OnExit hooks).
	internal static class ListenerParsing
	{
		internal const string ParameterEntityIdSentinel = "@param:";

		public static IReadOnlyList<ListenerInfo> GetListeners(TransformContext ctx,
			BehaviourDto behaviourDto) =>
			behaviourDto.Listeners
				.EmptyIfNull()
				.Select(l =>
				{
					var outputs = l.Outputs ?? new Dictionary<string, string>();

					if (l is GameOverListenerDto)
					{
						return new GameOverListenerInfo { OutputMapping = outputs };
					}

					if (l is { EntityTag: not null, BehaviourTag: not null })
					{
						throw new ParsingException(
							"A listener cannot declare both EntityTag and BehaviourTag. " +
							"Pick one: EntityTag (+ BehaviourId) targets behaviours on entities with that tag; " +
							"BehaviourTag targets all behaviours carrying that tag.");
					}

					if (l.EntityTag != null)
					{
						var entityTag = ValueSourceFactory.CreateValueSource<string>(ctx,
							AssemblerValueConverter.ToAssemblerValue(l.EntityTag));

						// A null BehaviourId is preserved (not coerced to ""): it means "fan out to every
						// behaviour on entities carrying this tag", per CLAUDE.md's "optionally filtered by
						// behaviour ID". The wiring picks GetByEntityTag vs GetByEntityTagAndBehaviourId on it.
						return new EntityTaggedListenerInfo(entityTag, l.BehaviourId) { OutputMapping = outputs };
					}

					if (l.BehaviourTag != null)
					{
						var behaviourTag = ValueSourceFactory.CreateValueSource<string>(ctx,
							AssemblerValueConverter.ToAssemblerValue(l.BehaviourTag));

						return (ListenerInfo)new BehaviourTaggedListenerInfo(behaviourTag) { OutputMapping = outputs };
					}

					var entityId = l.EntityId switch
					{
						ParamRefDto paramRefDto => ctx.Parameters.TryGetValue(paramRefDto.Id ?? string.Empty, out var pv)
												   && pv is StringValue sv
							? sv.Value
							: ParameterEntityIdSentinel + (paramRefDto.Id ?? string.Empty),
						VarRefDto varRefDto => varRefDto.ResolveValue<string>(ctx.Values),
						string behaviourId => behaviourId,
						_ => throw new ParsingException($"Cannot get Id for listener {l.EntityId}")
					};

					var behaviourDescriptor = new BehaviourDescriptor(entityId, l.BehaviourId ?? string.Empty);

					return new DirectListenerInfo(behaviourDescriptor) { OutputMapping = outputs };
				})
				.ToArray();

		/// <summary>
		/// Parses a behaviour's <c>Targets:</c> property — the set of behaviours it acts on (e.g. to
		/// enable/disable them). Reuses the nested-listener shape (direct EntityId + BehaviourId, EntityTag,
		/// or BehaviourTag) but requires at least one target and rejects <c>!gameover</c>, which is not a
		/// toggleable behaviour.
		/// </summary>
		public static IReadOnlyList<ListenerInfo> ParseTargets(TransformContext ctx, AssemblerValue? raw, string behaviourId)
		{
			var targets = ParseNestedListeners(ctx, raw ?? NoValue.Instance);

			if (targets.Count == 0)
			{
				throw new ParsingException(
					$"Behaviour '{behaviourId}': Targets must list at least one behaviour to act on " +
					"(by EntityId + BehaviourId, EntityTag, or BehaviourTag).");
			}

			if (targets.Any(t => t is GameOverListenerInfo))
			{
				throw new ParsingException(
					$"Behaviour '{behaviourId}': Targets cannot include !gameover — it is not a toggleable behaviour.");
			}

			return targets;
		}

		/// <summary>
		/// Builds listeners authored *inside* a behaviour's properties (e.g. a state machine's per-state
		/// <c>OnEnter</c>/<c>OnExit</c> hooks). Unlike the top-level <c>Listeners:</c> field, these arrive
		/// as already-converted <see cref="AssemblerValue"/>s (<see cref="DictValue"/> entries, or a
		/// <see cref="GameOverMarker"/> for a nested <c>!gameover</c>), so this mirrors <see cref="GetListeners"/>
		/// reading from that shape to produce identical <see cref="ListenerInfo"/> semantics.
		/// </summary>
		public static IReadOnlyList<ListenerInfo> ParseNestedListeners(TransformContext ctx, AssemblerValue raw) =>
			raw is ListValue list
				? list.Items.Select(item => ParseNestedListener(ctx, item)).ToArray()
				: Array.Empty<ListenerInfo>();

		private static ListenerInfo ParseNestedListener(TransformContext ctx, AssemblerValue item)
		{
			if (item is GameOverMarker)
			{
				return new GameOverListenerInfo();
			}

			if (item is not DictValue dict)
			{
				throw new ParsingException(
					"OnEnter/OnExit entries must be listener maps (EntityId + BehaviourId, EntityTag, or BehaviourTag) or !gameover.");
			}

			var fields = dict.Value;
			var outputs = ParseNestedOutputMapping(fields.GetValueOrDefault("Outputs") ?? NoValue.Instance);

			var hasEntityTag = fields.TryGetValue("EntityTag", out var entityTagValue);
			var hasBehaviourTag = fields.TryGetValue("BehaviourTag", out var behaviourTagValue);

			if (hasEntityTag && hasBehaviourTag)
			{
				throw new ParsingException(
					"A listener cannot declare both EntityTag and BehaviourTag. " +
					"Pick one: EntityTag (+ BehaviourId) targets behaviours on entities with that tag; " +
					"BehaviourTag targets all behaviours carrying that tag.");
			}

			// Null (omitted) BehaviourId is preserved for the entity-tag fan-out (see GetListeners); the
			// direct-descriptor path below still coerces it to "" since a direct listener targets one behaviour.
			var behaviourId = (fields.GetValueOrDefault("BehaviourId") as StringValue)?.Value;

			if (hasEntityTag)
			{
				return new EntityTaggedListenerInfo(ValueSourceFactory.CreateValueSource<string>(ctx, entityTagValue!), behaviourId)
				{
					OutputMapping = outputs
				};
			}

			if (hasBehaviourTag)
			{
				return new BehaviourTaggedListenerInfo(ValueSourceFactory.CreateValueSource<string>(ctx, behaviourTagValue!))
				{
					OutputMapping = outputs
				};
			}

			var entityId = ResolveNestedEntityId(ctx, fields.GetValueOrDefault("EntityId") ?? NoValue.Instance);

			return new DirectListenerInfo(new BehaviourDescriptor(entityId, behaviourId ?? string.Empty)) { OutputMapping = outputs };
		}

		private static IReadOnlyDictionary<string, string> ParseNestedOutputMapping(AssemblerValue value) =>
			value is DictValue dict
				? dict.Value.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value as StringValue)?.Value ?? string.Empty)
				: new Dictionary<string, string>();

		private static string ResolveNestedEntityId(TransformContext ctx, AssemblerValue value) =>
			value switch
			{
				StringValue s => s.Value,
				ParamRef p => ctx.Parameters.TryGetValue(p.Id, out var pv) && pv is StringValue sv
					? sv.Value
					: ParameterEntityIdSentinel + p.Id,
				VarRef v => ctx.Values.ResolveValue(v.Id) is StringValue sv
					? sv.Value
					: throw new ParsingException($"Listener EntityId variable '{v.Id}' must resolve to a string."),
				NoValue => throw new ParsingException(
					"Listener entry requires an EntityId (with BehaviourId), or an EntityTag / BehaviourTag."),
				_ => throw new ParsingException($"Cannot interpret listener EntityId '{value}'.")
			};
	}
}
