namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class IntLessThanOrEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue <= compareValue;
	}
}