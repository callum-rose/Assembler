using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Turns an entity each frame to face a world-space <c>Target</c> in the XZ ground plane (a pure yaw about
	/// +Y), the declarative form of the <c>LookRotationXZ</c> helper. With <c>TurnRate</c> 0 it snaps; a positive
	/// rate (degrees/sec) eases the turn so the entity rotates toward the target instead of jumping.
	/// </summary>
	public record LookAtInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Target,
		ValueSource<float> TurnRate) : BehaviourInfo(Id, Listeners)
	{
		public static LookAtInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			if (!props.ContainsKey("Target"))
			{
				throw new ParsingException($"look at '{id}': 'Target' is required (the point to face).");
			}

			return new LookAtInfo(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Target")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("TurnRate"), 0f));
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new LookAtInfo(Id,
				substitutedListeners,
				Target.SubstituteParameters(ctx),
				TurnRate.SubstituteParameters(ctx));
	}
}
