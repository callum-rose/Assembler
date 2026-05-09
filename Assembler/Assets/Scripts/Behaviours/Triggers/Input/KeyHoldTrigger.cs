using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Input
{
	public partial class KeyHoldTrigger : InputTrigger<KeyHoldTriggerInfo>
	{
		private KeyCode _keyCode;

		protected override void OnInitialise(KeyHoldTriggerInfo behaviourInfo)
		{
		}

		private void Update()
		{
			if (UnityEngine.Input.GetKey(_keyCode))
			{
				Execute();
			}
		}
	}
}