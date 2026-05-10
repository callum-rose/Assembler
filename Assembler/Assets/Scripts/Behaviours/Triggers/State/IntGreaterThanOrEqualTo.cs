namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class IntGreaterThanOrEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue >= compareValue;
	}
}