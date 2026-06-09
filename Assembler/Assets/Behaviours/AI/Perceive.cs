using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Sensor that scans for the nearest tagged entity and writes the result into blackboard variables.</summary>
	/// <remarks>
	/// The keystone of the perception layer: each scan finds the nearest entity carrying <c>Tag</c> within
	/// <c>Radius</c> (optionally limited to a forward cone and/or gated on line of sight) and writes the outcome
	/// into the named entity variables, which the FSM and steering layers then read as plain <c>!var</c>s.
	///
	/// Memory falls out of one rule: while a target is visible all outputs are written, including
	/// <c>LastKnownPositionVar</c>; when the target drops out of range/sight only <c>HasTargetVar</c> is set
	/// false and the others are left untouched — so "search where you last saw them" needs no extra state.
	/// Properties:
	///   Tag: Entity tag to look for.
	///   Radius: Detection range in world units.
	///   ConeAngle: Optional full cone angle in degrees; omit for an omnidirectional scan. Needs Forward.
	///   Forward: Optional facing direction for the cone (a direction vector).
	///   RequireLineOfSight: When true, a candidate is only detected if no obstacle blocks the line to it.
	///   Obstacles: Entity tag that blocks line of sight (empty means nothing blocks).
	///   Interval: Seconds between scans; 0 scans every frame. Trades responsiveness for cost.
	///   TargetIdVar: Name of the string variable that receives the detected entity id.
	///   TargetPositionVar: Name of the vector variable that receives the detected entity position.
	///   HasTargetVar: Name of the bool variable set true while a target is visible, false otherwise.
	///   LastKnownPositionVar: Name of the vector variable updated ONLY while visible (the memory of last sighting).
	/// </remarks>
	public sealed class Perceive : GameBehaviour<PerceiveData>, INeedsEntityQuery, INeedsLineOfSight, INeedsGameClock
	{
		public EntityQueryService Query { get; set; } = null!;
		public LineOfSightService Sight { get; set; } = null!;
		public IGameClock Clock { get; set; } = null!;

		private float _sinceLastScan;

		private void Update()
		{
			var interval = Data.Interval.Get(TriggerContext.Empty);

			_sinceLastScan += Clock.DeltaTime;

			if (interval > 0f && _sinceLastScan < interval)
			{
				return;
			}

			_sinceLastScan = 0f;
			Scan();
		}

		public override void Execute(TriggerContext ctx) => Scan();

		private void Scan()
		{
			var ctx = TriggerContext.Empty;
			var self = transform.position;
			var tag = Data.Tag.Get(ctx);
			var radius = Data.Radius.Get(ctx);

			var targetId = FindTarget(self, tag, radius, ctx);

			if (targetId != null && Data.RequireLineOfSight.Get(ctx) &&
				!Sight.CanSee(self, Query.PositionOf(targetId), Data.Obstacles.Get(ctx)))
			{
				targetId = null;
			}

			if (targetId == null)
			{
				// Lost the target: flip the flag but keep target_id / target_position / last_known_position so
				// downstream behaviours can steer toward the last sighting.
				Data.HasTarget?.Set(false);
				return;
			}

			var position = Query.PositionOf(targetId);
			Data.TargetId?.Set(targetId);
			Data.TargetPosition?.Set(position);
			Data.LastKnownPosition?.Set(position);
			Data.HasTarget?.Set(true);
		}

		private string? FindTarget(Vector3 self, string tag, float radius, TriggerContext ctx)
		{
			if (Data.ConeAngle is null || Data.Forward is null)
			{
				return Query.Nearest(self, tag, radius);
			}

			var forward = Data.Forward.Get(ctx);
			var halfAngle = Data.ConeAngle.Get(ctx) * 0.5f;
			var candidates = Query.WithinCone(self, forward, tag, radius, halfAngle);

			string? best = null;
			var bestSqr = float.PositiveInfinity;

			foreach (var id in candidates)
			{
				var sqr = (Query.PositionOf(id) - self).sqrMagnitude;

				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					best = id;
				}
			}

			return best;
		}
	}
}
