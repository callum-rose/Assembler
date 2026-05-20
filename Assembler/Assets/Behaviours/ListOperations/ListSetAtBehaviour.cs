using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Index: Zero-based position to overwrite.
	///   Value: New item.
	/// </remarks>
	public abstract class ListSetAtBehaviour<T> : GameBehaviour<ListSetAtData<T>>
	{
		public override void Execute()
		{
			var list = Data.List.Value;
			var index = Data.Index.Value;

			if (index >= 0 && index < list.Count)
			{
				list[index] = Data.Value.Value;
			}
		}
	}
}
