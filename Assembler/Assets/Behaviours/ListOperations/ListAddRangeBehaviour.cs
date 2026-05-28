using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Appends every item from Other to List when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Other: List whose items will be appended to List.
	/// </remarks>
	public abstract class ListAddRangeBehaviour<T> : GameBehaviour<ListAddRangeData<T>>
	{
		public override void Execute()
		{
			Data.List.Value.AddRange(Data.Other.Value);
		}
	}
}
