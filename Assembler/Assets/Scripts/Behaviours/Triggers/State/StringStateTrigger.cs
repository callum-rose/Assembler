
namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public abstract class StringVariableTrigger : CompareVariableTrigger<string>
	{
		protected override void OnEitherVariableChanged()
		{
			if (Compare(Variable.Value, OtherVariable.Value))
			{
				Execute();
			}
		}

		protected abstract bool Compare(string stateValue, string compareValue);
	}
	
	public class StringEquals : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => stateValue == compareValue;
	}
	
	public class StringNotEquals : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => stateValue != compareValue;
	}

	public class StringContains : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => stateValue.Contains(compareValue);
	}
	
	public class StringNotContains : StringVariableTrigger
	{
		protected override bool Compare(string stateValue, string compareValue) => !stateValue.Contains(compareValue);
	}
}