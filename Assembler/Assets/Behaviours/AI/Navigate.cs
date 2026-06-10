using System;
using System.Collections.Generic;
using Assembler.Libraries;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Moves an entity to a target along a grid path, recomputed on a cadence.</summary>
	/// <remarks>
	/// Steers along the waypoints of an A* path through the nav grid (Mode "astar"), easing in with
	/// <c>Arrive</c> on the final leg; or, for shared-goal swarms (Mode "flowfield"), follows the cached flow
	/// field toward the goal. The route is recomputed every <c>Recompute</c> seconds so a moving target is
	/// tracked. The interface is identical to the Phase-2 straight-line form, so swapping the route source
	/// needed no descriptor change. Writes to the <c>Output</c> velocity variable, or drives the entity directly.
	/// Properties:
	///   Target: World point to navigate to.
	///   Speed: Movement speed in units per second.
	///   SlowingRadius: Distance from the goal at which to begin easing to a stop.
	///   Recompute: Seconds between route recomputes (0 recomputes every frame).
	///   Mode: "astar" (per-agent path) or "flowfield" (shared-goal field).
	///   Output: Name of the vector variable to write the desired velocity into (omit to move the entity directly).
	/// </remarks>
	public sealed class Navigate : GameBehaviour<NavigateData>, INeedsGameClock, INeedsNavigation
	{
		public IGameClock Clock { get; set; } = null!;
		public NavGridService Nav { get; set; } = null!;

		private float _sinceRecompute;
		private IReadOnlyList<Vector3> _path = Array.Empty<Vector3>();
		private int _pathIndex;
		private bool _hasRoute;

		private void Update() => Execute(TriggerContext.Empty);

		public override void Execute(TriggerContext ctx)
		{
			var self = transform.position;
			var target = Data.Target.Get(ctx);
			var speed = Data.Speed.Get(ctx);
			var slowingRadius = Data.SlowingRadius.Get(ctx);
			var recompute = Data.Recompute.Get(ctx);

			_sinceRecompute += Clock.DeltaTime;

			var desired = Data.Mode.Get(ctx) == "flowfield"
				? FollowFlowField(self, target, speed, slowingRadius)
				: FollowPath(self, target, speed, slowingRadius, recompute);

			if (Data.Output is NullValueProvider<Vector3>)
			{
				transform.position += desired * Clock.DeltaTime;
			}
			else
			{
				Data.Output.Set(desired);
			}
		}

		private Vector3 FollowFlowField(Vector3 self, Vector3 target, float speed, float slowingRadius)
		{
			// Ease in once inside the slowing radius; otherwise ride the field toward the goal.
			if (Vector3.Distance(self, target) <= slowingRadius)
			{
				return SteeringMath.Arrive(self, target, speed, slowingRadius);
			}

			var flow = Nav.FlowDirection(self, target);

			// A zero direction means this cell has no field entry (unreachable, or off the grid). Fall back to
			// heading straight at the target — matching the astar path's raw-target fallback — rather than
			// freezing in place.
			return flow == Vector3.zero
				? SteeringMath.Seek(self, target, speed)
				: flow * speed;
		}

		private Vector3 FollowPath(Vector3 self, Vector3 target, float speed, float slowingRadius, float recompute)
		{
			if (!_hasRoute || recompute <= 0f || _sinceRecompute >= recompute)
			{
				_path = Nav.Path(self, target);
				// Skip the start cell (path[0] is the agent's own cell) so a fresh route heads to the next
				// waypoint rather than stalling on the current cell centre — important when recomputing often.
				_pathIndex = _path.Count > 1 ? 1 : 0;
				_hasRoute = true;
				_sinceRecompute = 0f;
			}

			// Nav.Path always yields at least the goal, so _path is non-empty and _pathIndex is in range here.
			var reach = Mathf.Max(0.05f, Nav.CellSize * 0.5f);

			while (_pathIndex < _path.Count - 1 && Vector3.Distance(self, _path[_pathIndex]) <= reach)
			{
				_pathIndex++;
			}

			var waypoint = _path[_pathIndex];

			// Full speed toward intermediate waypoints; ease to a stop only at the final goal.
			return _pathIndex == _path.Count - 1
				? SteeringMath.Arrive(self, waypoint, speed, slowingRadius)
				: SteeringMath.Seek(self, waypoint, speed);
		}
	}
}
