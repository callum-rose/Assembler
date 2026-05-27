using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Writes the current count of List into the variable referenced by Length when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Length: Reference to the destination int variable that will receive the list's current count.
	/// </remarks>
	public abstract class ListLengthBehaviour<T> : GameBehaviour<ListLengthData<T>>
	{
		public override void Execute()
		{
			Data.Length.Value = Data.List.Value.Count;
		}
	}
}
