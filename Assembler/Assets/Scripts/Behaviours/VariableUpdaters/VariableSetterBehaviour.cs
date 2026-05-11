using Assembler.Core;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Behaviours.VariableUpdaters
{
	public abstract class VariableSetterBehaviour<TValue> : GameBehaviour<VariableSetterData<TValue>>
	{
		public override void Execute()
		{
			Data.ValueToSet.Value = Data.ValueToGet.Value;
			Debug.Log($"{_id} set to {Data.ValueToSet.Value}");
		}
	}
}