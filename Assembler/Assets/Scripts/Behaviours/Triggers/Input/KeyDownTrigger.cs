using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Input
{
	public partial class KeyDownTrigger : InputTrigger<KeyDownTriggerInfo>
	{
		private KeyCode _keyCode;

		protected override void OnInitialise(KeyDownTriggerInfo behaviourInfo)
		{
		}

		private void Update()
		{
			if (UnityEngine.Input.GetKeyDown(_keyCode))
			{
				Execute();
			}
		}
	}
}