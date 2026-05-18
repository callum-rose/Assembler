using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.VariableUpdaters
{
	public abstract class VariableSetterBehaviour<TValue> : GameBehaviour<VariableSetterData<TValue>>
	{
		public override void Execute()
		{
			Data.ValueToSet.Value = Data.ValueToGet.Value;
			UnityEngine.Debug.Log($"{Id} set to {Data.ValueToSet.Value}");
		}
	}
}