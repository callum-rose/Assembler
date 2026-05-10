using System;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using AssemblerAlpha.Variables;

namespace AssemblerAlpha.Behaviours.VariableUpdaters
{
	public abstract class VariableSetterBehaviour<TInfo, TValue> : GameBehaviour<TInfo> where TInfo : BehaviourInfo
	{
		private Func<TValue> _valueGetter;
		private GameVariable<TValue> _variable;

		public override void Execute()
		{
			_variable.Value = _valueGetter.Invoke();
		}
	}

}