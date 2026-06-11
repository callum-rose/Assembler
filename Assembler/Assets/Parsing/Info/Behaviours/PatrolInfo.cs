using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Waypoint sequencer + mover: walks an entity through an ordered list of points, advancing to the next
	/// once the current one is reached, under a Loop / PingPong / one-shot policy. Models the
	/// <c>navigate</c>/<c>steering</c> interface — desired velocity out to <c>Output</c>, or direct transform
	/// motion when unbound — so it slots in as the "patrol" state of a perceive→FSM AI. <c>Waypoints</c> is a
	/// vector-list (a <c>!var</c> reference, an inline list, or an <c>!expr</c> building a <c>PositionList</c>).
	/// </summary>
	public record PatrolInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<Vector3>> Waypoints,
		ValueSource<bool> Loop,
		ValueSource<bool> PingPong,
		ValueSource<float> ArriveRadius,
		ValueSource<float> Speed,
		ValueSource<Vector3> Output,
		ValueSource<int> CurrentIndex) : BehaviourInfo(Id, Listeners)
	{
		public static PatrolInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			if (!props.ContainsKey("Waypoints"))
			{
				throw new ParsingException(
					$"patrol '{id}': 'Waypoints' is required (the ordered points to patrol).");
			}

			return new PatrolInfo(id,
				listeners,
				ValueSourceFactory.CreateValueSource<List<Vector3>>(ctx, props.GetValueOrDefault("Waypoints")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Loop"), true),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("PingPong"), false),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("ArriveRadius"), 0.2f),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Speed"), 3f),
				// Optional: unset resolves to a null provider and the agent moves its own transform directly.
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Output")),
				// Optional: unset resolves to a null provider and the current index is not published anywhere.
				ValueSourceFactory.CreateOptionalValueSource<int>(ctx, props.GetValueOrDefault("CurrentIndex")));
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new PatrolInfo(Id,
				substitutedListeners,
				Waypoints.SubstituteParameters(ctx),
				Loop.SubstituteParameters(ctx),
				PingPong.SubstituteParameters(ctx),
				ArriveRadius.SubstituteParameters(ctx),
				Speed.SubstituteParameters(ctx),
				Output.SubstituteParameters(ctx),
				CurrentIndex.SubstituteParameters(ctx));
	}
}
