using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes the item at Index from List when Executed. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Index: Zero-based position to remove from.
	/// </remarks>
	public abstract class ListRemoveAtBehaviour<T> : GameBehaviour<ListRemoveAtData<T>>
	{
		public override void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			var index = Data.Index.Get(ctx);

			if (index >= 0 && index < list.Count)
			{
				list.RemoveAt(index);
			}
		}
	}
}
