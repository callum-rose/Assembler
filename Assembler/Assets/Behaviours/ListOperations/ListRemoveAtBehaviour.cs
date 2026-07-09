using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes the item at Index from List when Executed. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Index: Zero-based position to remove from.
	/// </remarks>
	public abstract class ListRemoveAtBehaviour<T> : GameBehaviour<ListRemoveAtData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			var index = Data.Index.Get(ctx);

			if (index >= 0 && index < list.Count)
			{
				list.RemoveAt(index);
			}
		}
	}

	/// <summary>Removes the bool item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class BoolListRemoveAt : ListRemoveAtBehaviour<bool> { }

	/// <summary>Removes the Color item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class ColourListRemoveAt : ListRemoveAtBehaviour<Color> { }

	/// <summary>Removes the float item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class FloatListRemoveAt : ListRemoveAtBehaviour<float> { }

	/// <summary>Removes the int item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class IntListRemoveAt : ListRemoveAtBehaviour<int> { }

	/// <summary>Removes the record item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class RecordListRemoveAt : ListRemoveAtBehaviour<Record> { }

	/// <summary>Removes the string item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class StringListRemoveAt : ListRemoveAtBehaviour<string> { }

	/// <summary>Removes the Vector3 item at a given index from the target list when Executed. See <see cref="ListRemoveAtBehaviour{T}"/>.</summary>
	public class Vector3ListRemoveAt : ListRemoveAtBehaviour<Vector3> { }
}
