using System.Collections.Generic;
using Assembler.Libraries;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Walks an entity through an ordered list of waypoints, advancing on arrival.</summary>
	/// <remarks>
	/// The "default idle" locomotion for a patrol↔chase AI: head toward the current waypoint with
	/// <c>Arrive</c> easing and, once within <c>ArriveRadius</c>, advance to the next one under the chosen
	/// policy — <c>Loop</c> wraps back to the start, <c>PingPong</c> reverses at the ends (overriding
	/// <c>Loop</c>), and with neither it runs the route once and holds at the last point. Mirrors
	/// <c>navigate</c>/<c>steering</c>: the desired velocity is written to the <c>Output</c> variable for an
	/// integrator (e.g. <c>velocity</c>) to apply, or, when no output is named, integrated onto the entity
	/// transform directly. (Chosen the mover shape over a pure goal-emitting sequencer so one behaviour is the
	/// whole patrol state; compose with an integrator via <c>Output</c> when you need shared-velocity
	/// blending.) Drop it under a <c>state machine</c> as the patrol state and let <c>perceive</c> flip the
	/// FSM to chase when a target appears.
	/// Properties:
	///   Waypoints: Ordered world points to patrol (a vector-list !var, an inline list, or an !expr PositionList).
	///   Loop: Wrap back to the first waypoint after the last (patrol loop) instead of stopping.
	///   PingPong: Reverse direction at each end instead of wrapping (overrides Loop).
	///   ArriveRadius: Distance at which the current waypoint counts as reached and the index advances.
	///   Speed: Movement speed in units per second.
	///   Output: Name of the vector variable to write the desired velocity into (omit to move the entity directly).
	///   CurrentIndex: Name of an int variable to publish the current waypoint index into (omit to skip; for FSM/debug).
	/// </remarks>
	public sealed class Patrol : GameBehaviour<PatrolData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private int _index;
		private int _direction = 1;

		private void Update() => Step();

		internal void Step()
		{
			var waypoints = Data.Waypoints.Get();

			// Empty (or unset) route: nothing to patrol — hold position and emit zero velocity.
			if (waypoints is null || waypoints.Count == 0)
			{
				PublishIndex();
				MoveOrEmit(Vector3.zero);
				return;
			}

			var arriveRadius = Data.ArriveRadius.Get();
			_index = Mathf.Clamp(_index, 0, waypoints.Count - 1);

			var self = transform.position;

			// Reached the current waypoint -> step the index to the next per the Loop/PingPong policy.
			if (Vector3.Distance(self, waypoints[_index]) <= arriveRadius)
			{
				Advance(waypoints.Count);
			}

			PublishIndex();

			// Arrive eases to a stop at a held waypoint (a one-shot path's final point); for looping/intermediate
			// waypoints the index has already advanced above, so this just heads on toward the new target.
			var desired = SteeringMath.Arrive(self, waypoints[_index], Data.Speed.Get(), arriveRadius);
			MoveOrEmit(desired);
		}

		private void Advance(int count)
		{
			// A single waypoint has nowhere to advance to; hold on it.
			if (count <= 1)
			{
				return;
			}

			if (Data.PingPong.Get())
			{
				// Reflect off either end, then step one in the (possibly flipped) direction.
				if (_index + _direction < 0 || _index + _direction > count - 1)
				{
					_direction = -_direction;
				}

				_index += _direction;
			}
			else if (Data.Loop.Get())
			{
				_index = (_index + 1) % count;
			}
			else if (_index < count - 1)
			{
				_index++;
			}
		}

		private void PublishIndex()
		{
			if (Data.CurrentIndex is not NullValueProvider<int>)
			{
				Data.CurrentIndex.Set(_index);
			}
		}

		private void MoveOrEmit(Vector3 desired)
		{
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
