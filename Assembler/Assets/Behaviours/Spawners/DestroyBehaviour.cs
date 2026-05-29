using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
    /// <summary>Destroys the entity's GameObject when Executed and notifies any listeners.</summary>
    /// <remarks>
    /// Properties:
    /// </remarks>
    public class DestroyBehaviour : GameBehaviour<DestroyData>
    {
        public override void Execute(TriggerContext ctx)
        {
            Destroy(gameObject);
            NotifyListeners(ctx);
        }
    }
}
