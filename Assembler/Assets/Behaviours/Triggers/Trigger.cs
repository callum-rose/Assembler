using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers
{
	public abstract class Trigger<T> : GameBehaviour<T>, INeedsTriggerContext where T : TriggerData
	{
		public TriggerContext TriggerContext { get; set; }
	}
}