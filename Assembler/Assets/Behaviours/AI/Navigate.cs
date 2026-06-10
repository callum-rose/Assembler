using Assembler.Libraries;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Moves an entity toward a target, easing to a stop on arrival.</summary>
	/// <remarks>
	/// This Phase-2 form treats the route as a straight line to the target and steers with <c>Arrive</c>; the
	/// target is re-read on a cadence so a moving goal is tracked. The interface (target in, motion out,
	/// recompute cadence, mode) is fixed so grid pathfinding can be slotted under it without re-authoring any
	/// descriptor. <c>Mode</c> ("astar"/"flowfield") is recorded now and consulted once the nav grid exists.
	/// Writes to the <c>Output</c> velocity variable, or drives the entity directly.
	/// Properties:
	///   Target: World point to navigate to.
	///   Speed: Movement speed in units per second.
	///   SlowingRadius: Distance from the goal at which to begin easing to a stop.
	///   Recompute: Seconds between route recomputes (0 recomputes every frame).
	///   Mode: "astar" or "flowfield" — selects the grid path source once a nav grid is present.
	///   Output: Name of the vector variable to write the desired velocity into (omit to move the entity directly).
	/// </remarks>
	public sealed class Navigate : GameBehaviour<NavigateData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private float _sinceRecompute;
		private Vector3 _waypoint;
		private bool _hasWaypoint;

		private void Update() => Execute(TriggerContext.Empty);

		public override void Execute(TriggerContext ctx)
		{
			var self = transform.position;
			var recompute = Data.Recompute.Get(ctx);

			_sinceRecompute += Clock.DeltaTime;

			if (!_hasWaypoint || recompute <= 0f || _sinceRecompute >= recompute)
			{
				// Phase 2: the route is the target itself. Phase 3 replaces this with the next path waypoint.
				_waypoint = Data.Target.Get(ctx);
				_hasWaypoint = true;
				_sinceRecompute = 0f;
			}

			var desired = SteeringMath.Arrive(self, _waypoint, Data.Speed.Get(ctx), Data.SlowingRadius.Get(ctx));

			if (Data.Output is NullValueProvider<Vector3>)
			{
				transform.position += desired * Clock.DeltaTime;
			}
			else
			{
				Data.Output.Set(desired);
			}
		}
	}
}
