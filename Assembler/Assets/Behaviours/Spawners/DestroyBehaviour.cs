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
        public IEntitySpawner Spawner { get; set; }

        public override void Execute()
        {
            NotifyListeners();

            var entity = GetComponent<GameEntity>();
            if (Spawner != null && entity != null)
            {
                Spawner.Despawn(entity);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
