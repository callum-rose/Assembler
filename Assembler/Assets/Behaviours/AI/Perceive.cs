using System.Collections;
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
	/// into the bound entity variables, which the FSM and steering layers then read as plain <c>!var</c>s.
	///
	/// Memory falls out of one rule: while a target is visible all outputs are written, including
	/// <c>LastKnownPosition</c>; when the target drops out of range/sight only <c>HasTarget</c> is set false and
	/// the others are left untouched — so "search where you last saw them" needs no extra state.
	/// Properties:
	///   Tag: Entity tag to look for.
	///   Radius: Detection range in world units.
	///   ConeAngle: Optional full cone angle in degrees; omit for an omnidirectional scan. Needs Forward.
	///   Forward: Optional facing direction for the cone (a direction vector).
	///   RequireLineOfSight: When true, a candidate is only detected if no obstacle blocks the line to it.
	///   Obstacles: Entity tag that blocks line of sight (empty means nothing blocks).
	///   Interval: Seconds between scans; 0 scans every frame. Trades responsiveness for cost.
	///   TargetId: !var reference to the string variable that receives the detected entity id.
	///   TargetPosition: !var reference to the vector variable that receives the detected entity position.
	///   HasTarget: !var reference to the bool variable set true while a target is visible, false otherwise.
	///   LastKnownPosition: !var reference to the vector variable updated ONLY while visible (memory of last sighting).
	/// </remarks>
	public sealed class Perceive : GameBehaviour<PerceiveData>, INeedsEntityQuery, INeedsLineOfSight, INeedsGameClock
	{
		public EntityQueryService Query { get; set; } = null!;
		public LineOfSightService Sight { get; set; } = null!;
		public IGameClock Clock { get; set; } = null!;

		// The owning entity, read so the scan can exclude this entity's own id (see TryFindTarget). Resolved
		// from the sibling component rather than gameObject.name so it tracks the descriptor id explicitly.
		private GameEntity _entity = null!;

		private bool ConeConfigured =>
			Data.ConeAngle is not NullValueProvider<float> && Data.Forward is not NullValueProvider<Vector3>;

		private void Start() => StartCoroutine(ScanLoop());

		public override void Execute(TriggerContext ctx) => Scan();

		protected override void OnInitialise(PerceiveData data) => _entity = GetComponent<GameEntity>();

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

			var found = TryFindTarget(self, Data.Tag.Get(ctx), Data.Radius.Get(ctx), ctx, out var targetId);

			if (!found || !Query.TryGetPosition(targetId, out var position) ||
				(Data.RequireLineOfSight.Get(ctx) && !Sight.CanSee(self, position, Data.Obstacles.Get(ctx))))
			{
				// Lost the target: flip the flag but keep target_id / target_position / last_known_position so
				// downstream behaviours can steer toward the last sighting.
				Data.HasTarget.Set(false);
				return;
			}

			Data.TargetId.Set(targetId);
			Data.TargetPosition.Set(position);
			Data.LastKnownPosition.Set(position);
			Data.HasTarget.Set(true);
		}

		private bool TryFindTarget(Vector3 self, string tag, float radius, TriggerContext ctx, out string targetId)
		{
			// Exclude this entity from its own scan: a same-tag perceive would otherwise always detect itself at
			// distance 0, making separation/nearest-ally relationships impossible.
			var selfId = _entity.Id;

			if (!ConeConfigured)
			{
				return Query.TryNearest(self, tag, radius, selfId, out targetId);
			}

			var candidates = Query.WithinCone(self, Data.Forward.Get(ctx), tag, radius, Data.ConeAngle.Get(ctx) * 0.5f, selfId);

			targetId = string.Empty;
			var found = false;
			var bestSqr = float.PositiveInfinity;

			foreach (var id in candidates)
			{
				if (!Query.TryGetPosition(id, out var position))
				{
					continue;
				}

				var sqr = (position - self).sqrMagnitude;

				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					targetId = id;
					found = true;
				}
			}

			return found;
		}
	}
}
