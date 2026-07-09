using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
	/// <summary>Destroys the entity's GameObject when Executed and notifies any listeners.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class DestroyBehaviour : GameBehaviour<DestroyData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			// Evict from the spatial-query index synchronously. Destroy(gameObject) is deferred to end of frame, and
			// the index otherwise only deregisters via GameEntity.OnDestroy (also end of frame), so anything that
			// queries the index later this same frame — a downstream `tag count` recount, a `perceive`, a `!query` —
			// would still see this dying entity. Unregister is a no-op on an unknown id, so the end-of-frame
			// OnDestroy deregistration remains safe.
			Entity.Query?.Unregister(Entity.Id);
			Destroy(gameObject);
			NotifyListeners(ctx);
		}
	}
}
