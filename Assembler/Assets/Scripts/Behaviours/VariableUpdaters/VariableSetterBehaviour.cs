using System;
using Assembler.Parsing2.Info;
using Core;
using Variables;

namespace Behaviours.VariableUpdaters
{
	public abstract class VariableSetterBehaviour<T> : GameBehaviour<T> where T : BehaviourInfo
	{
		private Func<T> _valueGetter;
		private GameVariable<T> _variable;

		public override void Execute()
		{
			_variable.Value = _valueGetter.Invoke();
		}
	}

	public class StringSetter : VariableSetterBehaviour<string> { }

	public class IntSetter : VariableSetterBehaviour<int> { }

	public class FloatSetter : VariableSetterBehaviour<float> { }

	public class BoolSetter : VariableSetterBehaviour<bool> { }
}