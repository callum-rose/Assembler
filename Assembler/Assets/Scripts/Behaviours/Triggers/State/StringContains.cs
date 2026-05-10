namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class StringContains : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => stateValue.Contains(compareValue);
	}
}