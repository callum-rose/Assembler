using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours
{
	/// <summary>Ends and unloads the running game when Executed, then notifies any listeners.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class EndGame : GameBehaviour<EndGameData>
	{
		public override void Execute(TriggerContext ctx)
		{
			GetComponentInParent<GameController>()?.EndGame();
			NotifyListeners(ctx);
		}
	}
}
