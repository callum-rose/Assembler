using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public abstract class FloatVariableTrigger : CompareVariableTrigger<float>
	{
		protected override void OnEitherVariableChanged()
		{
			if (Compare(Variable.Value, OtherVariable.Value))
			{
				Execute();
			}
		}

		protected abstract bool Compare(float stateValue, float compareValue);
	}

	public class FloatEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => Mathf.Approximately(stateValue, compareValue);
	}

	public class FloatNotEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => !Mathf.Approximately(stateValue, compareValue);
	}

	public class FloatGreaterThan : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue > compareValue;
	}

	public class FloatLessThan : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue < compareValue;
	}

	public class FloatGreaterThanOrEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue >= compareValue;
	}

	public class FloatLessThanOrEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue <= compareValue;
	}
}