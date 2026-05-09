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

	public class StringSetter : VariableSetterBehaviour<StringVariableSetterInfo, string>
	{
		protected override void OnInitialise(StringVariableSetterInfo behaviourInfo)
		{
		}
	}

	public class IntSetter : VariableSetterBehaviour<IntVariableSetterInfo, int>
	{
		protected override void OnInitialise(IntVariableSetterInfo behaviourInfo)
		{
		}
	}

	public class FloatSetter : VariableSetterBehaviour<FloatVariableSetterInfo, float>
	{
		protected override void OnInitialise(FloatVariableSetterInfo behaviourInfo)
		{
		}
	}

	public class BoolSetter : VariableSetterBehaviour<BoolVariableSetterInfo, bool>
	{
		protected override void OnInitialise(BoolVariableSetterInfo behaviourInfo)
		{
		}
	}
}