namespace Assembler.Behaviours.VariableUpdaters
{
	public class IntAdjust : VariableAdjustBehaviour<int>
	{
		protected override int Add(int current, int delta) => current + delta;
	}
}
