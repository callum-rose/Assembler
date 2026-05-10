namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class IntEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue == compareValue;
	}
}