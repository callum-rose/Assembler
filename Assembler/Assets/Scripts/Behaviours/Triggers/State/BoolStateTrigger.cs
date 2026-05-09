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
	
	public class BoolIsFalse : VariableTrigger<bool>
	{
		protected override void VariableOnChanged()
		{
			if (!Variable.Value)
			{
				Execute();
			}
		}
	}

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