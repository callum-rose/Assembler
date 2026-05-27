namespace Assembler.Behaviours.VariableUpdaters
{
	public class FloatAdjust : VariableAdjustBehaviour<float>
	{
		protected override float Add(float current, float delta) => current + delta;
	}
}
