
namespace AssemblerAlpha.Behaviours.Triggers.State
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

}