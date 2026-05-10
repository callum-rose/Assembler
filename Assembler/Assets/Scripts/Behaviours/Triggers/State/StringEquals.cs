namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class StringEquals : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => stateValue == compareValue;
	}
}