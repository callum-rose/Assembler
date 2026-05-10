namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public abstract class FloatVariableTrigger : CompareVariableTrigger<float>
	{
		protected override void OnEitherVariableChanged()
		{
			if (Compare(Variable.Value, OtherVariable.Value))
			{
				Execute();
			}
		}

		protected abstract bool Compare(float stateValue, float compareValue);
	}

}