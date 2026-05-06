
namespace Behaviours.Triggers.Variable
{
	public abstract class IntVariableTrigger : CompareVariableTrigger<int>
	{
		protected override void OnEitherVariableChanged()
		{
			if (Compare(Variable.Value, OtherVariable.Value))
			{
				Execute();
			}
		}
		
		protected abstract bool Compare(int stateValue, int compareValue);
	}
	
	public class IntEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue == compareValue;
	}
	
	public class IntNotEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue != compareValue;
	}
	
	public class IntGreaterThan : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue > compareValue;
	}
	
	public class IntLessThan : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue < compareValue;
	}
	
	public class IntGreaterThanOrEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue >= compareValue;
	}
	
	public class IntLessThanOrEqualTo : IntVariableTrigger
	{
		protected override bool Compare(int stateValue, int compareValue) => stateValue <= compareValue;
	}
}