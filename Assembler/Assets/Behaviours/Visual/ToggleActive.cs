using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Flips the entity GameObject's active state each time it is Executed by an upstream trigger.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class ToggleActive : GameBehaviour<ToggleActiveData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			gameObject.SetActive(!gameObject.activeSelf);
		}
	}
}
