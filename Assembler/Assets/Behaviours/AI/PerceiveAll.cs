using System.Collections;
using System.Collections.Generic;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Sensor that scans for every tagged entity in range and writes them into blackboard list variables.</summary>
	/// <remarks>
	/// The multi-target counterpart to <c>perceive</c>: where that finds the single nearest target, this fans the
	/// whole set of entities carrying <c>Tag</c> within <c>Radius</c> (optionally limited to a forward cone and/or
	/// gated on line of sight) into list variables. Each scan clears and repopulates the lists, so they always
	/// reflect the current neighbourhood — the input the flocking forces (<c>Separate</c>/<c>Cohesion</c>/
	/// <c>Alignment</c>) consume via <c>steering</c>.
	///
	/// <c>Positions</c>, <c>Ids</c> and <c>Velocities</c> are parallel: index <c>i</c> of each describes the same
	/// detected entity. <c>Velocities</c> are finite-differenced between scans, so an entity contributes zero
	/// velocity on the first scan it appears in. Any omitted output is skipped.
	/// Properties:
	///   Tag: Entity tag to look for.
	///   Radius: Detection range in world units.
	///   ConeAngle: Optional full cone angle in degrees; omit for an omnidirectional scan. Needs Forward.
	///   Forward: Optional facing direction for the cone (a direction vector).
	///   RequireLineOfSight: When true, a candidate is only detected if no obstacle blocks the line to it.
	///   Obstacles: Entity tag that blocks line of sight (empty means nothing blocks).
	///   Interval: Seconds between scans; 0 scans every frame. Trades responsiveness for cost.
	///   Positions: !var reference to the vector-list variable cleared and filled with each detected entity's position.
	///   Ids: !var reference to the string-list variable cleared and filled with each detected entity's id.
	///   Velocities: !var reference to the vector-list variable cleared and filled with each detected entity's velocity (finite-differenced between scans).
	///   Count: !var reference to the int variable set to the number of entities detected this scan.
	/// </remarks>
	public sealed class PerceiveAll : GameBehaviour<PerceiveAllData>, INeedsEntityQuery, INeedsLineOfSight, INeedsGameClock
	{
		public EntityQueryService Query { get; set; } = null!;
		public LineOfSightService Sight { get; set; } = null!;
		public IGameClock Clock { get; set; } = null!;

		// Per-id baseline for finite-differencing velocities. Two buffers swapped each scan so the baseline only
		// ever holds entities visible last scan (no leak as neighbours churn). Only maintained when Velocities is wired.
		private Dictionary<string, Vector3> _previousPositions = new();
		private Dictionary<string, Vector3> _sampledPositions = new();
		private double _lastSampleTime;

		private bool ConeConfigured =>
			Data.ConeAngle is not NullValueProvider<float> && Data.Forward is not NullValueProvider<Vector3>;

		private bool WritePositions => Data.Positions is not NullValueProvider<List<Vector3>>;
		private bool WriteIds => Data.Ids is not NullValueProvider<List<string>>;
		private bool WriteVelocities => Data.Velocities is not NullValueProvider<List<Vector3>>;

		private void Start() => StartCoroutine(ScanLoop());

		public override void Execute(TriggerContext ctx) => Scan();

		private IEnumerator ScanLoop()
		{
			while (true)
			{
				Scan();

				var interval = Data.Interval.Get(TriggerContext.Empty);
				yield return interval > 0f ? new WaitForGameSeconds(Clock, interval) : null;
			}
		}

		private void Scan()
		{
			var ctx = TriggerContext.Empty;
			var self = transform.position;
			var tag = Data.Tag.Get(ctx);
			var radius = Data.Radius.Get(ctx);

			// Exclude this entity from its own scan: a same-tag query would otherwise always detect itself at
			// distance 0, polluting separation/alignment with a zero-offset neighbour.
			var selfId = Entity.Id;

			var candidates = ConeConfigured
				? Query.WithinCone(self, Data.Forward.Get(ctx), tag, radius, Data.ConeAngle.Get(ctx) * 0.5f, selfId)
				: Query.WithinRadius(self, tag, radius, selfId);

			var requireSight = Data.RequireLineOfSight.Get(ctx);
			var obstacles = requireSight ? Data.Obstacles.Get(ctx) : string.Empty;

			var positions = WritePositions ? Data.Positions.Get(ctx) : null;
			var ids = WriteIds ? Data.Ids.Get(ctx) : null;
			var velocities = WriteVelocities ? Data.Velocities.Get(ctx) : null;

			positions?.Clear();
			ids?.Clear();
			velocities?.Clear();

			var dt = velocities != null ? (float)(Clock.Time - _lastSampleTime) : 0f;
			var count = 0;

			foreach (var id in candidates)
			{
				if (!Query.TryGetPosition(id, out var position) ||
					(requireSight && !Sight.CanSee(self, position, obstacles)))
				{
					continue;
				}

				positions?.Add(position);
				ids?.Add(id);

				if (velocities != null)
				{
					var velocity = dt > 0f && _previousPositions.TryGetValue(id, out var previous)
						? (position - previous) / dt
						: Vector3.zero;

					velocities.Add(velocity);
					_sampledPositions[id] = position;
				}

				count++;
			}

			if (velocities != null)
			{
				// Promote this scan's samples to next scan's baseline; clear the now-stale buffer for reuse.
				(_previousPositions, _sampledPositions) = (_sampledPositions, _previousPositions);
				_sampledPositions.Clear();
				_lastSampleTime = Clock.Time;
			}

			Data.Count.Set(count);
		}
	}
}
