using Assembler.Resolving;

namespace Assembler.Behaviours.Movement
{
	public class Translate : TransformBehaviour<TranslateData>
	{
		public override void Execute()
		{
			transform.position += Data.Displacement.Value;
		}
	}
}