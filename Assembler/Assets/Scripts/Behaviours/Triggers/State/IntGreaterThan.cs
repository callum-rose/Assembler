namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class IntGreaterThan : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue > compareValue;
	}
}