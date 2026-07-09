using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes all items from List when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	/// </remarks>
	public abstract class ListClearBehaviour<T> : GameBehaviour<ListClearData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			Data.List.Get(ctx).Clear();
		}
	}

	/// <summary>Removes all items from the target bool list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class BoolListClear : ListClearBehaviour<bool> { }

	/// <summary>Removes all items from the target Color list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class ColourListClear : ListClearBehaviour<Color> { }

	/// <summary>Removes all items from the target float list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class FloatListClear : ListClearBehaviour<float> { }

	/// <summary>Removes all items from the target int list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class IntListClear : ListClearBehaviour<int> { }

	/// <summary>Removes all items from the target record list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class RecordListClear : ListClearBehaviour<Record> { }

	/// <summary>Removes all items from the target string list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class StringListClear : ListClearBehaviour<string> { }

	/// <summary>Removes all items from the target Vector3 list when Executed. See <see cref="ListClearBehaviour{T}"/>.</summary>
	public class Vector3ListClear : ListClearBehaviour<Vector3> { }
}
