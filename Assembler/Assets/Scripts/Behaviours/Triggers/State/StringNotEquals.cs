namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class StringNotEquals : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => stateValue != compareValue;
	}
}