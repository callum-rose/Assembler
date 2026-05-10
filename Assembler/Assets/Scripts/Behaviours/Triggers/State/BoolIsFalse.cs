namespace AssemblerAlpha.Behaviours.Triggers.State
{
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
}