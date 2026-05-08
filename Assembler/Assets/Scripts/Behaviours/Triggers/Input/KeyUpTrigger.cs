using Core;
using UnityEngine;

namespace Behaviours.Triggers.Input
{
	public partial class KeyUpTrigger : InputTrigger
	{
			private KeyCode _keyCode;

		private void Update()
		{
			if (UnityEngine.Input.GetKeyUp(_keyCode))
			{
				Execute();
			}
		}
	}
}