namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class FloatGreaterThanOrEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue >= compareValue;
	}
}