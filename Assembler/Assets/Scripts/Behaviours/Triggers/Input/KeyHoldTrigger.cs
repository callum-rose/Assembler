using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Input
{
	public partial class KeyHoldTrigger : InputTrigger
	{
		[Inject("Key")] private KeyCode _keyCode;

		private void Update()
		{
			if (UnityEngine.Input.GetKey(_keyCode))
			{
				Execute();
			}
		}
	}
}