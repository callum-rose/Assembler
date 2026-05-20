using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes all items from List when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	/// </remarks>
	public abstract class ListClearBehaviour<T> : GameBehaviour<ListClearData<T>>
	{
		public override void Execute()
		{
			Data.List.Value.Clear();
		}
	}
}
