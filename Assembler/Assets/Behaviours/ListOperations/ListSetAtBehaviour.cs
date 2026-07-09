using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Index: Zero-based position to overwrite.
	///   Value: New item.
	/// </remarks>
	public abstract class ListSetAtBehaviour<T> : GameBehaviour<ListSetAtData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			var index = Data.Index.Get(ctx);

			if (index >= 0 && index < list.Count)
			{
				list[index] = Data.Value.Get(ctx);
			}
		}
	}

	/// <summary>Overwrites the bool item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class BoolListSetAt : ListSetAtBehaviour<bool> { }

	/// <summary>Overwrites the Color item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class ColourListSetAt : ListSetAtBehaviour<Color> { }

	/// <summary>Overwrites the float item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class FloatListSetAt : ListSetAtBehaviour<float> { }

	/// <summary>Overwrites the int item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class IntListSetAt : ListSetAtBehaviour<int> { }

	/// <summary>Overwrites the record item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class RecordListSetAt : ListSetAtBehaviour<Record> { }

	/// <summary>Overwrites the string item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class StringListSetAt : ListSetAtBehaviour<string> { }

	/// <summary>Overwrites the Vector3 item at a given index in the target list when Executed. See <see cref="ListSetAtBehaviour{T}"/>.</summary>
	public class Vector3ListSetAt : ListSetAtBehaviour<Vector3> { }
}
