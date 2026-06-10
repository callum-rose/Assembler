using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
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
	/// </summary>
	public sealed class GridMover : GameBehaviour<GridMoverData>, INeedsGameClock, INeedsNavigation
	{
		// Snap distance for "arrived at the target cell". MoveTowards lands exactly on the target once within a
		// frame's step, so any small epsilon catches the arrival frame.
		private const float ArrivalEpsilon = 0.01f;

		public IGameClock Clock { get; set; } = null!;
		public NavGridService Nav { get; set; } = null!;

		private bool _initialised;
		private Vector3 _target;
		private Vector3 _heading = Vector3.zero;

		private void Update() => Execute(TriggerContext.Empty);

		public override void Execute(TriggerContext ctx)
		{
			var pos = transform.position;
			var cellSize = Nav.CellSize;

			if (!_initialised)
			{
				_target = Nav.CellCentre(pos);
				_initialised = true;
			}

			// A jump larger than a cell means something else moved us (e.g. a wrap-position tunnel); re-anchor
			// to the cell we now occupy rather than steering all the way back to the stale target.
			var reanchor = cellSize * 1.5f;
			if ((pos - _target).sqrMagnitude > reanchor * reanchor)
			{
				_target = Nav.CellCentre(pos);
			}

			// Centred on the target cell: pick the next cell to head for.
			if ((pos - _target).sqrMagnitude <= ArrivalEpsilon * ArrivalEpsilon)
			{
				transform.position = _target;
				pos = _target;

				var desired = Nav.CardinalStep(Data.Direction.Get(ctx));

				// Turn onto the requested heading when the cell that way is open; otherwise keep going straight.
				if (desired != Vector3.zero && Nav.IsWalkable(Nav.CellCentre(pos + desired * cellSize)))
				{
					_heading = desired;
				}

				if (_heading != Vector3.zero && Nav.IsWalkable(Nav.CellCentre(pos + _heading * cellSize)))
				{
					_target = Nav.CellCentre(pos + _heading * cellSize);
				}
				else
				{
					// Blocked ahead: stop until a clear direction is requested at this cell.
					_heading = Vector3.zero;
				}
			}

			var step = Data.Speed.Get(ctx) * Clock.DeltaTime;
			transform.position = Vector3.MoveTowards(transform.position, _target, step);
		}
	}
}
