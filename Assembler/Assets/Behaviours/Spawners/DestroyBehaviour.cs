using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
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
