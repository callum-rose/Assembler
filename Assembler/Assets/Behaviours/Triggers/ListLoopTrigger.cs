using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers
{
	/// <summary>Iterates List synchronously when Executed, firing listeners once per element.</summary>
	/// <remarks>
	/// Properties:
	///   List: Reference to the list to iterate over.
	/// Outputs:
	///   item [T]: The current element of the list.
	///   index [int]: Zero-based position of the current element.
	/// </remarks>
	public abstract class ListLoopTrigger<T> : Trigger<ListLoopTriggerData<T>>
	{
		public override void Execute()
		{
			var list = Data.List.Value;

			for (int i = 0; i < list.Count; i++)
			{
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("item", list[i]);
					TriggerContext.Set("index", i);
					NotifyListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
			}
		}
	}
}
