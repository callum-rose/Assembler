using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
	/// <summary>Despatches the entity when Executed (returning it to the pool if it was spawned, else destroying
	/// its GameObject) and notifies any listeners.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class DestroyBehaviour : GameBehaviour<DestroyData>, INeedsEntitySink, IAmExecutable
	{
		public IEntitySink Sink { get; set; } = null!;

		public void Execute(TriggerContext ctx)
		{
			Sink.Despawn(Entity);
			NotifyListeners(ctx);
		}
	}
}
