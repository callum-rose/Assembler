using Assembler.Behaviours.Triggers;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Reads the item at Index in List when Executed and publishes it as the <c>value</c> output, then notifies listeners. No-op if Index is out of range.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the source list variable.
	///   Index: Zero-based position to read.
	/// Outputs:
	///   value [T]: The item at the given index.
	/// </remarks>
	public abstract class ListGetAtBehaviour<T> : Trigger<ListGetAtData<T>>
	{
		public override void Execute()
		{
			var list = Data.List.Value;
			var index = Data.Index.Value;

			if (index < 0 || index >= list.Count)
			{
				return;
			}

			TriggerContext.Push();
			try
			{
				TriggerContext.Set("value", (object)list[index]);
				NotifyListeners();
			}
			finally
			{
				TriggerContext.Pop();
			}
		}
	}
}
