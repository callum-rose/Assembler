using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Physical
{
	public class TriggerEnter : PhysicalTrigger<TriggerEnterTriggerInfo>
	{
		protected override void OnInitialise(TriggerEnterTriggerInfo behaviourInfo)
		{
		}

		private void OnTriggerEnter(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				InvokeTrigger();
			}
		}
	}
}