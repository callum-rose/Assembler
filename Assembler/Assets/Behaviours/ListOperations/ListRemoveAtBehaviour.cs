using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	public abstract class ListRemoveAtBehaviour<T> : GameBehaviour<ListRemoveAtData<T>>
	{
		public override void Execute()
		{
			var list = Data.List.Value;
			var index = Data.Index.Value;

			if (index >= 0 && index < list.Count)
			{
				list.RemoveAt(index);
			}
		}
	}
}
