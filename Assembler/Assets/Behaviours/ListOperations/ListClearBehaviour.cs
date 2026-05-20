using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	public abstract class ListClearBehaviour<T> : GameBehaviour<ListClearData<T>>
	{
		public override void Execute()
		{
			Data.List.Value.Clear();
		}
	}
}
