using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Sets the entity GameObject's active state to the Active value when Executed by an upstream trigger.</summary>
	/// <remarks>
	/// Properties:
	///   Active: Boolean applied to the entity's active state on each Execute; true activates, false deactivates.
	/// </remarks>
	public class SetActive : GameBehaviour<SetActiveData>
	{
		public override void Execute(TriggerContext ctx)
		{
			gameObject.SetActive(Data.Active.Get(ctx));
		}
	}
}
