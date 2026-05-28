using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes the first occurrence of Value from List when Executed. No-op if Value is not present.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Value: Item to remove.
	/// </remarks>
	public abstract class ListRemoveBehaviour<T> : GameBehaviour<ListRemoveData<T>>
	{
		public override void Execute()
		{
			Data.List.Value.Remove(Data.Value.Value);
		}
	}
}
