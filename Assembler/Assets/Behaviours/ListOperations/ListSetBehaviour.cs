using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Replaces every item in List with the items from Value when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Value: List whose items replace List's contents (typically an expression returning a list).
	/// </remarks>
	public abstract class ListSetBehaviour<T> : GameBehaviour<ListSetData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);
			list.Clear();
			list.AddRange(Data.Value.Get(ctx));
		}
	}

	/// <summary>Replaces the entire contents of the target bool list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class BoolListSet : ListSetBehaviour<bool> { }

	/// <summary>Replaces the entire contents of the target Color list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class ColourListSet : ListSetBehaviour<Color> { }

	/// <summary>Replaces the entire contents of the target float list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class FloatListSet : ListSetBehaviour<float> { }

	/// <summary>Replaces the entire contents of the target int list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class IntListSet : ListSetBehaviour<int> { }

	/// <summary>Replaces the entire contents of the target record list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class RecordListSet : ListSetBehaviour<Record> { }

	/// <summary>Replaces the entire contents of the target string list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class StringListSet : ListSetBehaviour<string> { }

	/// <summary>Replaces the entire contents of the target Vector3 list with another list when Executed. See <see cref="ListSetBehaviour{T}"/>.</summary>
	public class Vector3ListSet : ListSetBehaviour<Vector3> { }
}
