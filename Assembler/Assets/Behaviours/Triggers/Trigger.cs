using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers
{
	public abstract class Trigger<T> : GameBehaviour<T> where T : TriggerData
	{
		public override void Execute() { }

		protected void InvokeListeners()
		{
			foreach (var listener in Data.Listeners)
			{
				listener();
			}
		}
	}
}