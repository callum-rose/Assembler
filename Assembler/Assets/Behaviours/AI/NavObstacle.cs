using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Makes the entity a dynamic nav-grid obstacle whose cells block or free at runtime, on Execute.</summary>
	/// <remarks>
	/// This is how destructible or moving terrain works with <c>grid mover</c> / <c>navigate</c> — a bombed soft
	/// block, an opening door, a rising wall or a pushed crate — where the baked static grid (the <c>Navigation</c>
	/// <c>ObstacleTag</c>) never changes. The cells this entity's colliders cover are blocked or freed on the shared
	/// <see cref="NavGridService"/> synchronously, at build and again each Execute. Unlike a tagged static wall a
	/// dynamic obstacle is declared by this behaviour and is <b>not</b> baked into the static grid, so it must not
	/// also carry the obstacle tag. On Execute it re-reads <c>Blocked</c> and recomputes its footprint from its
	/// current position, so wiring the trigger that destroys a block to also Execute this (with <c>Blocked: false</c>)
	/// opens the path the moment the block dies — no waiting for end-of-frame teardown. The overlay is refcounted, so
	/// overlapping obstacles reopen a cell only once the last of them leaves it.
	/// Properties:
	///   Blocked: Whether this obstacle currently blocks its cells; re-read on each Execute (bind to a variable/expression to toggle). Defaults to true.
	/// </remarks>
	public sealed class NavObstacle : GameBehaviour<NavObstacleData>, INeedsNavigation, IAmExecutable
	{
		public NavGridService Nav { get; set; } = null!;

		public void Execute(TriggerContext ctx)
		{
			Apply(ctx);
			NotifyListeners(ctx);
		}

		protected override void OnInitialise(NavObstacleData data)
		{
			// Register the initial state at build time (OnInitialise runs in the sandbox and on spawn, Awake/Start
			// do not) so an obstacle present or spawned as blocked shapes the grid before anything navigates it.
			Apply(TriggerContext.Empty);
		}

		private void OnDestroy()
		{
			// Leak-guard: withdraw this obstacle's footprint if it is destroyed without first being toggled off,
			// so a torn-down block never strands a permanently blocked cell. The gameplay-critical open/close
			// still goes through Execute; this end-of-frame cleanup is only a safety net.
			Nav?.RemoveObstacle(this);
		}

		private void Apply(TriggerContext ctx) =>
			Nav.SetObstacle(this, Footprint(), Data.Blocked.ValueOr(ctx, true));

		// The world-space bounds this obstacle occupies — the union of its colliders, or a degenerate point at its
		// position when it has none (still a single cell). NavGridService maps this onto the grid lattice.
		private Bounds Footprint()
		{
			// Collider.bounds lags the transform until the physics scene syncs, and play mode leaves
			// Physics.autoSyncTransforms off — so at build time (before the first physics step) a freshly
			// positioned obstacle's bounds can still read its pre-placement pose, registering the footprint on a
			// stale cell (a cell the block isn't visually on: an invisible wall). Sync first so bounds are current.
			// EditMode/tests don't hit this (bounds sync on read there), which is why it only shows in a real run.
			// Fully qualified: the Assembler.Behaviours.Physics namespace shadows UnityEngine.Physics here.
			UnityEngine.Physics.SyncTransforms();

			var colliders = GetComponentsInChildren<Collider>();

			if (colliders.Length == 0)
			{
				return new Bounds(transform.position, Vector3.zero);
			}

			var bounds = colliders[0].bounds;

			for (var i = 1; i < colliders.Length; i++)
			{
				bounds.Encapsulate(colliders[i].bounds);
			}

			return bounds;
		}
	}
}
