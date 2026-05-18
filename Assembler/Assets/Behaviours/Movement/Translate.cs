using Assembler.Core;
using Assembler.Resolving;

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