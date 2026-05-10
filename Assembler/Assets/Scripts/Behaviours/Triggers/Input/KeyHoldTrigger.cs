using Assembler.Parsing.Phase3;

namespace Assembler.Behaviours.Triggers.Input
{
	public class KeyHoldTrigger : InputTrigger<KeyHoldTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKey(Data.Key.Value))
			{
				Execute();
			}
		}
	}
}