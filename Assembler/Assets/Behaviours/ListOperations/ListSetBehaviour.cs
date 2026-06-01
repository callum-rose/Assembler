using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Replaces every item in List with the items from Value when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Value: List whose items replace List's contents (typically an expression returning a list).
	/// </remarks>
	public abstract class ListSetBehaviour<T> : GameBehaviour<ListSetData<T>>
	{
		public override void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			list.Clear();
			list.AddRange(Data.Value.Get(ctx));
		}
	}
}
