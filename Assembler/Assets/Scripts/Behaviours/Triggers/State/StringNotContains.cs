namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class StringNotContains : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => !stateValue.Contains(compareValue);
	}
}