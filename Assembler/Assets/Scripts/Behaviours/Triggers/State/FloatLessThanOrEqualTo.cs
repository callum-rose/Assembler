namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class FloatLessThanOrEqualTo : FloatVariableTrigger
	{
		protected override bool Compare(float stateValue, float compareValue) => stateValue <= compareValue;
	}
}