namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class IntLessThan : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue < compareValue;
	}
}