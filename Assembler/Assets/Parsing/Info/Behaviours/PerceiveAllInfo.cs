using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// The multi-target perception sensor's parsed configuration. <see cref="Tag"/>/<see cref="Radius"/> are
	/// required; the cone (<see cref="Forward"/> + <see cref="ConeAngle"/>) and line-of-sight gating are optional.
	/// Unlike <see cref="PerceiveInfo"/> the outputs are <c>!var</c> references to <em>list</em> variables that the
	/// sensor clears and repopulates each scan (<see cref="Positions"/>, <see cref="Ids"/>, <see cref="Velocities"/>)
	/// plus an optional scalar <see cref="Count"/>; an omitted output resolves to a null-object provider and is
	/// simply not written.
	/// </summary>
	public record PerceiveAllInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Tag,
		ValueSource<float> Radius,
		ValueSource<float> ConeAngle,
		ValueSource<Vector3> Forward,
		ValueSource<bool> RequireLineOfSight,
		ValueSource<string> Obstacles,
		ValueSource<float> Interval,
		ValueSource<List<Vector3>> Positions,
		ValueSource<List<string>> Ids,
		ValueSource<List<Vector3>> Velocities,
		ValueSource<int> Count) : BehaviourInfo(Id, Listeners)
	{
		public static PerceiveAllInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			if (!props.ContainsKey("Tag"))
			{
				throw new ParsingException($"perceive all '{id}': 'Tag' is required (the entity tag to look for).");
			}

			if (!props.ContainsKey("Radius"))
			{
				throw new ParsingException($"perceive all '{id}': 'Radius' is required (the detection range).");
			}

			return new PerceiveAllInfo(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Tag")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("ConeAngle")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Forward")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("RequireLineOfSight"), false),
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Obstacles"), string.Empty),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Interval"), 0f),
				ValueSourceFactory.CreateOptionalValueSource<List<Vector3>>(ctx, props.GetValueOrDefault("Positions")),
				ValueSourceFactory.CreateOptionalValueSource<List<string>>(ctx, props.GetValueOrDefault("Ids")),
				ValueSourceFactory.CreateOptionalValueSource<List<Vector3>>(ctx, props.GetValueOrDefault("Velocities")),
				ValueSourceFactory.CreateOptionalValueSource<int>(ctx, props.GetValueOrDefault("Count")));
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new PerceiveAllInfo(Id,
				substitutedListeners,
				Tag.SubstituteParameters(ctx),
				Radius.SubstituteParameters(ctx),
				ConeAngle.SubstituteParameters(ctx),
				Forward.SubstituteParameters(ctx),
				RequireLineOfSight.SubstituteParameters(ctx),
				Obstacles.SubstituteParameters(ctx),
				Interval.SubstituteParameters(ctx),
				Positions.SubstituteParameters(ctx),
				Ids.SubstituteParameters(ctx),
				Velocities.SubstituteParameters(ctx),
				Count.SubstituteParameters(ctx));
	}
}
