
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	public class KeyUpTrigger : InputTrigger<KeyUpTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKeyUp(Data.Key.Value))
			{
				NotifyListeners();
			}
		}
	}
}