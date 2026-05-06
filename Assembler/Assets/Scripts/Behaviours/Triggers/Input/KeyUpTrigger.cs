using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Input
{
	public partial class KeyUpTrigger : InputTrigger
	{
		[Inject("Key")] 	private KeyCode _keyCode;

		private void Update()
		{
			if (UnityEngine.Input.GetKeyUp(_keyCode))
			{
				Execute();
			}
		}
	}
}