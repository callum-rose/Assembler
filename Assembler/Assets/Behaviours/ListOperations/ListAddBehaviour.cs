using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Appends Value to the end of List when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the target list variable.
	///   Value: Item to append.
	/// </remarks>
	public abstract class ListAddBehaviour<T> : GameBehaviour<ListAddData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			Data.List.Get(ctx).Add(Data.Value.Get(ctx));
		}
	}

	/// <summary>Appends a bool value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class BoolListAdd : ListAddBehaviour<bool> { }

	/// <summary>Appends a Color value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class ColourListAdd : ListAddBehaviour<Color> { }

	/// <summary>Appends a float value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class FloatListAdd : ListAddBehaviour<float> { }

	/// <summary>Appends an int value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class IntListAdd : ListAddBehaviour<int> { }

	/// <summary>Appends a record value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class RecordListAdd : ListAddBehaviour<Record> { }

	/// <summary>Appends a string value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class StringListAdd : ListAddBehaviour<string> { }

	/// <summary>Appends a Vector3 value to the end of the target list when Executed. See <see cref="ListAddBehaviour{T}"/>.</summary>
	public class Vector3ListAdd : ListAddBehaviour<Vector3> { }
}
