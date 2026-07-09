using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes the first occurrence of Value from List when Executed. No-op if Value is not present.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Value: Item to remove.
	/// </remarks>
	public abstract class ListRemoveBehaviour<T> : GameBehaviour<ListRemoveData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			Data.List.Get(ctx).Remove(Data.Value.Get(ctx));
		}
	}

	/// <summary>Removes the first occurrence of a bool value from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class BoolListRemove : ListRemoveBehaviour<bool> { }

	/// <summary>Removes the first occurrence of a Color value from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class ColourListRemove : ListRemoveBehaviour<Color> { }

	/// <summary>Removes the first occurrence of a float value from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class FloatListRemove : ListRemoveBehaviour<float> { }

	/// <summary>Removes the first occurrence of an int value from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class IntListRemove : ListRemoveBehaviour<int> { }

	/// <summary>Removes the first occurrence (by reference identity) of a record from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class RecordListRemove : ListRemoveBehaviour<Record> { }

	/// <summary>Removes the first occurrence of a string value from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class StringListRemove : ListRemoveBehaviour<string> { }

	/// <summary>Removes the first occurrence of a Vector3 value from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class Vector3ListRemove : ListRemoveBehaviour<Vector3> { }
}
