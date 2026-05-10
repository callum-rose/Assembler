namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class FloatLessThan : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue < compareValue;
	}
}