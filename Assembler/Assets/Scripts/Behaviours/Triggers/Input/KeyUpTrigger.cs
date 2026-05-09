using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Input
{
	public partial class KeyUpTrigger : InputTrigger<KeyUpTriggerInfo>
	{
		private KeyCode _keyCode;

		protected override void OnInitialise(KeyUpTriggerInfo behaviourInfo)
		{
		}

		private void Update()
		{
			if (UnityEngine.Input.GetKeyUp(_keyCode))
			{
				Execute();
			}
		}
	}
}