using Core;
using UnityEngine;

namespace Behaviours.Triggers.Input
{
	public partial class KeyHoldTrigger : InputTrigger
	{
		private KeyCode _keyCode;

		private void Update()
		{
			if (UnityEngine.Input.GetKey(_keyCode))
			{
				Execute();
			}
		}
	}
}