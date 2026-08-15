using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Flow
{
	/// <summary>Forwards an upstream trigger straight through to its own listeners, passing the trigger context on unchanged. A shared fan-out point: when several triggers all drive the same reaction, point each at one relay instead of repeating the identical Listeners list on every one.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public sealed class Relay : Trigger<RelayData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx) => NotifyListeners(ctx);
	}
}
