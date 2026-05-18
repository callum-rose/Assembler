using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Movement
{
	public class Translate : GameBehaviour<TranslateData>
	{
		public override void Execute()
		{
			transform.position += Data.Displacement.Value;
		}
	}
}