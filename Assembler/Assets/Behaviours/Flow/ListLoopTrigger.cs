using Assembler.Behaviours.Triggers;
using Assembler.Core;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Flow
{
	/// <summary>Iterates List synchronously when Executed, firing listeners once per element.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the list to iterate over.
	/// Outputs:
	///   item [T]: The current element of the list.
	///   index [int]: Zero-based position of the current element.
	/// </remarks>
	public abstract class ListLoopTrigger<T> : Trigger<ListLoopTriggerData<T>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var list = Data.List.Get(ctx);

			for (int i = 0; i < list.Count; i++)
			{
				var iteration = i;
				NotifyListeners(ctx.With(b =>
				{
					b["item"] = list[iteration]!;
					b["index"] = iteration;
				}));
			}
		}
	}

	/// <summary>Iterates a bool list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class BoolListLoopTrigger : ListLoopTrigger<bool> { }

	/// <summary>Iterates a Color list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class ColourListLoopTrigger : ListLoopTrigger<Color> { }

	/// <summary>Iterates a float list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class FloatListLoopTrigger : ListLoopTrigger<float> { }

	/// <summary>Iterates an int list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class IntListLoopTrigger : ListLoopTrigger<int> { }

	/// <summary>Iterates a record list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class RecordListLoopTrigger : ListLoopTrigger<Record> { }

	/// <summary>Iterates a string list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class StringListLoopTrigger : ListLoopTrigger<string> { }

	/// <summary>Iterates a Vector3 list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class Vector3ListLoopTrigger : ListLoopTrigger<Vector3> { }
}
