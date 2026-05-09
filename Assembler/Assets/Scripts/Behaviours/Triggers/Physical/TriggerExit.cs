using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Physical
{
	public class TriggerExit : PhysicalTrigger<TriggerExitTriggerInfo>
	{
		protected override void OnInitialise(TriggerExitTriggerInfo behaviourInfo)
		{
		}

		private void OnTriggerExit(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				Execute();
			}
		}
	}
}