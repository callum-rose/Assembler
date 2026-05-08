using Core;
using UnityEngine;

namespace Behaviours.Triggers.Input
{
	public partial class KeyDownTrigger : InputTrigger
	{
		private KeyCode _keyCode;

		private void Update()
		{
			if (UnityEngine.Input.GetKeyDown(_keyCode))
			{
				Execute();
			}
		}
	}
}