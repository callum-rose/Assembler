using System.Collections.Generic;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Replaces the contents of List with Count copies of Value when Executed. Negative Count clamps to zero, producing an empty list.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Count: Number of copies of Value to write (negative values clamp to 0).
	///   Value: Item to fill the list with.
	/// </remarks>
	public abstract class ListFillBehaviour<T> : GameBehaviour<ListFillData<T>>
	{
		public override void Execute()
		{
			Fill(Data.List.Value, Data.Count.Value, Data.Value.Value);
		}

		public static void Fill(IList<T> list, int count, T value)
		{
			list.Clear();

			if (count <= 0)
			{
				return;
			}

			for (var i = 0; i < count; i++)
			{
				list.Add(value);
			}
		}
	}
}
