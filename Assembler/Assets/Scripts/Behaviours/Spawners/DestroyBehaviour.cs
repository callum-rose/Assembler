using Assembler.Core;
using Assembler.Parsing.Phase3;

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
