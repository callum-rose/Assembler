namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class Or : CompareVariableTrigger<bool>
	{
		protected override void OnEitherVariableChanged()
		{
			if (Variable.Value || OtherVariable.Value)
			{
				Execute();
			}
		}
	}
}