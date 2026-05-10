using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class FloatEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => Mathf.Approximately(stateValue, compareValue);
	}
}