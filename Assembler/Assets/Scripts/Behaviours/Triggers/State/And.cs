namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class And : CompareVariableTrigger<bool>
	{
		protected override void OnEitherVariableChanged()
		{
			if (Variable.Value && OtherVariable.Value)
			{
				Execute();
			}
		}
	}
}