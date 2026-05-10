using Assembler.Core;
using Assembler.Parsing.Phase3;

namespace Assembler.Behaviours.VariableUpdaters
{
	public abstract class VariableSetterBehaviour<TValue> : GameBehaviour<VariableSetterData<TValue>>
	{
		public override void Execute()
		{
			Data.ValueContainer.Value = Data.ValueProvider.Value;
		}
	}
}