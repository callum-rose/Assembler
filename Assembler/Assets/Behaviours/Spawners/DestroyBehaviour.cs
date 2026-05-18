using Assembler.Resolving;

namespace Assembler.Behaviours.Spawners
{
    public class DestroyBehaviour : GameBehaviour<DestroyData>
    {
        public override void Execute()
        {
            Destroy(gameObject);
            NotifyListeners();
        }
    }
}
