
using Assembler.Resolving;

namespace Assembler.Behaviours.Triggers.Input
{
	public class KeyDownTrigger : InputTrigger<KeyDownTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKeyDown(Data.Key.Value))
			{
				InvokeListeners();
			}
		}
	}
}