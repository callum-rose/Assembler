namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public class BoolIsTrue : VariableTrigger<bool>
	{
		protected override void VariableOnChanged()
		{
			if (Variable.Value)
			{
				Execute();
			}
		}
	}

}