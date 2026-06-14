using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>
	/// Moves the entity tile-to-tile along the shared nav grid: it heads to the centre of the next cell, and
	/// only re-decides direction once it arrives there, so motion is always grid-aligned and never diagonal
	/// (classic maze movement). At each cell it turns onto the requested <c>Direction</c> if that neighbour is
	/// walkable, else continues its current heading, else stops. Walkability and cell geometry come from the
	/// <see cref="NavGridService"/>, so a player driven by this and the AI driven by <c>navigate</c> share one
	/// maze. Robust to external teleports (a <c>wrap position</c> tunnel): a large jump re-anchors to the new
	/// cell instead of dragging the entity back.
	/// Properties:
	///   Direction: Requested heading, re-read each frame (bind to a variable an input trigger writes); snapped to a cardinal.
	///   Speed: Movement speed in units per second.
	///   AgentRadius: Clearance used for walkability checks, in world units; omit to inherit the game-wide Navigation DefaultAgentRadius. Tile-locked movers usually leave this 0 (a one-cell agent).
	/// </summary>
	public sealed class GridMover : PerFrameBehaviour<GridMoverData>, INeedsNavigation
	{
		public NavGridService Nav { get; set; } = null!;

		private bool _initialised;
		private Vector3 _target;
		private Vector3 _heading = Vector3.zero;

		// Re-anchor to the spawn cell and drop the carried heading so a pooled reuse starts from its new position
		// rather than steering toward the previous life's target cell.
		public override void OnReuse()
		{
			_initialised = false;
			_heading = Vector3.zero;
		}

		internal override void Step()
		{
			var cellSize = Nav.CellSize;
			// Unset AgentRadius falls back to the game-wide Navigation DefaultAgentRadius.
			var agentRadius = Data.AgentRadius.ValueOr(Nav.DefaultAgentRadius);

			if (!_initialised)
			{
				_target = Nav.CellCentre(transform.position);
				_initialised = true;
			}

			// A jump larger than a cell means something else moved us (e.g. a wrap-position tunnel); re-anchor
			// to the cell we now occupy rather than steering all the way back to the stale target.
			var reanchor = cellSize * 1.5f;
			if ((transform.position - _target).sqrMagnitude > reanchor * reanchor)
			{
				_heading = Vector3.zero;
				_target = Nav.CellCentre(transform.position);
			}

			var desired = Nav.CardinalStep(Data.Direction.Get());

			// Instant U-turn: reversing along a corridor shouldn't wait for the next cell. Retarget the cell
			// behind us straight away so the entity turns around the moment the key is pressed.
			if (desired != Vector3.zero && desired == -_heading)
			{
				var back = Nav.CellCentre(transform.position + desired * cellSize);
				if (Nav.IsWalkable(back, agentRadius))
				{
					_heading = desired;
					_target = back;
				}
			}

			// Advance toward the target cell, and once reached re-decide the heading and carry the leftover
			// distance straight through the turn — so a turn taken at a cell centre keeps full speed rather
			// than stalling for a frame.
			var remaining = Data.Speed.Get() * Clock.DeltaTime;

			while (remaining > 0f)
			{
				var toTarget = Vector3.Distance(transform.position, _target);

				if (toTarget > remaining)
				{
					transform.position = Vector3.MoveTowards(transform.position, _target, remaining);
					break;
				}

				// Snap onto the cell centre and spend the distance it took to get here.
				transform.position = _target;
				remaining -= toTarget;

				// Turn onto the requested heading when the cell that way is open; otherwise keep going straight.
				if (desired != Vector3.zero && Nav.IsWalkable(Nav.CellCentre(_target + desired * cellSize), agentRadius))
				{
					_heading = desired;
				}

				if (_heading != Vector3.zero && Nav.IsWalkable(Nav.CellCentre(_target + _heading * cellSize), agentRadius))
				{
					_target = Nav.CellCentre(_target + _heading * cellSize);
				}
				else
				{
					// Blocked ahead: stop on this cell until a clear direction is requested.
					_heading = Vector3.zero;
					break;
				}
			}
		}
	}
}
