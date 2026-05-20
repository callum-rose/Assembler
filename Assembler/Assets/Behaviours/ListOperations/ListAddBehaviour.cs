using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.ListOperations
{
	public abstract class ListAddBehaviour<T> : GameBehaviour<ListAddData<T>>
	{
		public override void Execute()
		{
			Data.List.Value.Add(Data.Value.Value);
		}
	}
}
