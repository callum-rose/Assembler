using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Appends Value to the end of List when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Value: Item to append.
	/// </remarks>
	public abstract class ListAddBehaviour<T> : GameBehaviour<ListAddData<T>>
	{
		public override void Execute(TriggerContext ctx)
		{
			Data.List.Get(ctx).Add(Data.Value.Get(ctx));
		}
	}
}
