
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

}