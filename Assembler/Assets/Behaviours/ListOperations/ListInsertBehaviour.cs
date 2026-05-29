using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Inserts Value into List at Index when Executed. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Index: Zero-based position to insert at. Valid range is [0, Count].
	///   Value: Item to insert.
	/// </remarks>
	public abstract class ListInsertBehaviour<T> : GameBehaviour<ListInsertData<T>>
	{
		public override void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			var index = Data.Index.Get(ctx);

			if (index >= 0 && index <= list.Count)
			{
				list.Insert(index, Data.Value.Get(ctx));
			}
		}
	}
}
