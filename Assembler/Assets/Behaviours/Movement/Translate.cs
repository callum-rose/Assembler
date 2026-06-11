using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Adds Displacement to the entity's world position each time it Executes (e.g. via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Displacement: World-space offset to add on each execution.
	/// </remarks>
	public class Translate : GameBehaviour<TranslateData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			transform.position += Data.Displacement.Get(ctx);
		}
	}
}
