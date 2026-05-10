namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class FloatGreaterThan : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue > compareValue;
	}
}