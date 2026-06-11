using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

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
}
