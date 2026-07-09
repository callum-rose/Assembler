using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Appends every item from Other to List when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Other: List whose items will be appended to List.
	/// </remarks>
	public abstract class ListAddRangeBehaviour<T> : GameBehaviour<ListAddRangeData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			Data.List.Get(ctx).AddRange(Data.Other.Get(ctx));
		}
	}

	/// <summary>Appends every item from another bool list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class BoolListAddRange : ListAddRangeBehaviour<bool> { }

	/// <summary>Appends every item from another Color list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class ColourListAddRange : ListAddRangeBehaviour<Color> { }

	/// <summary>Appends every item from another float list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class FloatListAddRange : ListAddRangeBehaviour<float> { }

	/// <summary>Appends every item from another int list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class IntListAddRange : ListAddRangeBehaviour<int> { }

	/// <summary>Appends every item from another record list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class RecordListAddRange : ListAddRangeBehaviour<Record> { }

	/// <summary>Appends every item from another string list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class StringListAddRange : ListAddRangeBehaviour<string> { }

	/// <summary>Appends every item from another Vector3 list to the target list when Executed. See <see cref="ListAddRangeBehaviour{T}"/>.</summary>
	public class Vector3ListAddRange : ListAddRangeBehaviour<Vector3> { }
}
