using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Inserts Value into List at Index when Executed. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Index: Zero-based position to insert at. Valid range is [0, Count].
	///   Value: Item to insert.
	/// </remarks>
	public abstract class ListInsertBehaviour<T> : GameBehaviour<ListInsertData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			var index = Data.Index.Get(ctx);

			if (index >= 0 && index <= list.Count)
			{
				list.Insert(index, Data.Value.Get(ctx));
			}
		}
	}

	/// <summary>Inserts a bool value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class BoolListInsert : ListInsertBehaviour<bool> { }

	/// <summary>Inserts a Color value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class ColourListInsert : ListInsertBehaviour<Color> { }

	/// <summary>Inserts a float value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class FloatListInsert : ListInsertBehaviour<float> { }

	/// <summary>Inserts an int value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class IntListInsert : ListInsertBehaviour<int> { }

	/// <summary>Inserts a record value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class RecordListInsert : ListInsertBehaviour<Record> { }

	/// <summary>Inserts a string value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class StringListInsert : ListInsertBehaviour<string> { }

	/// <summary>Inserts a Vector3 value into the target list at a given index when Executed. See <see cref="ListInsertBehaviour{T}"/>.</summary>
	public class Vector3ListInsert : ListInsertBehaviour<Vector3> { }
}
