using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Tile-locked movement on the shared navigation grid: the entity glides cell-to-cell at <c>Speed</c>,
	/// only ever turning when centred on a cell, and never diagonally. <c>Direction</c> is the requested
	/// heading, re-read each frame (bind it to a variable an input trigger writes) — at each cell the mover
	/// turns onto it if the next cell that way is walkable, otherwise it keeps its current heading until a wall
	/// stops it. Walls come from the same <c>Navigation:</c> walkability grid the <c>navigate</c> behaviour
	/// uses, so player and AI agree on the maze.
	/// </summary>
	public record GridMoverInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Direction,
		ValueSource<float> Speed,
		ValueSource<float> AgentRadius)
		: BehaviourInfo(Id, Listeners)
	{
		public static GridMoverInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Direction")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Speed"), 4f),
				// Negative => inherit the game-wide Navigation AgentRadius. Tile-locked movers usually want 0 (a
				// one-cell agent); a larger value treats narrow gaps as blocked, like the navigate behaviour.
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("AgentRadius"), -1f));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new GridMoverInfo(Id,
				substitutedListeners,
				Direction.SubstituteParameters(ctx),
				Speed.SubstituteParameters(ctx),
				AgentRadius.SubstituteParameters(ctx));
	}
}
