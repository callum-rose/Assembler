using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

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
