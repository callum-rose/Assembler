using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Drives an entity toward a target, easing to a stop on arrival. The Phase-2 form treats the route as a
	/// straight line and steers with <c>Arrive</c>; the interface (target in, motion out, recompute cadence,
	/// mode) is locked so grid pathfinding can be slotted under it without re-authoring any descriptor.
	/// <c>Mode</c> ("astar"/"flowfield") is recorded now and consulted once the nav grid exists.
	/// </summary>
	public record NavigateInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Target,
		ValueSource<float> Speed,
		ValueSource<float> SlowingRadius,
		ValueSource<float> Recompute,
		ValueSource<string> Mode,
		ValueSource<float> AgentRadius,
		ValueSource<Vector3> Output) : BehaviourInfo(Id, Listeners)
	{
		public static NavigateInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			if (!props.ContainsKey("Target"))
			{
				throw new ParsingException($"navigate '{id}': 'Target' is required (the point to move toward).");
			}

			return new NavigateInfo(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Target")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Speed"), 3f),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("SlowingRadius"), 0.75f),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Recompute"), 0.25f),
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Mode"), "astar"),
				// Optional: when unset this resolves to a null provider and the agent falls back to the game-wide
				// Navigation DefaultAgentRadius at the point of use. Set it to give this agent its own clearance,
				// so a larger agent routes around obstacles more widely than a smaller one.
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("AgentRadius")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Output")));
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new NavigateInfo(Id,
				substitutedListeners,
				Target.SubstituteParameters(ctx),
				Speed.SubstituteParameters(ctx),
				SlowingRadius.SubstituteParameters(ctx),
				Recompute.SubstituteParameters(ctx),
				Mode.SubstituteParameters(ctx),
				AgentRadius.SubstituteParameters(ctx),
				Output.SubstituteParameters(ctx));
	}
}
