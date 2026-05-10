namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class IntNotEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue != compareValue;
	}
}