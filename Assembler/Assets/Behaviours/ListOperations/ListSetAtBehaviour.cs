using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	public abstract class ListSetAtBehaviour<T> : GameBehaviour<ListSetAtData<T>>
	{
		public override void Execute()
		{
			var list = Data.List.Value;
			var index = Data.Index.Value;

			if (index >= 0 && index < list.Count)
			{
				list[index] = Data.Value.Value;
			}
		}
	}
}
