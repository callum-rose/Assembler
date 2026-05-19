using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers
{
	public class ForEachTrigger : Trigger<ForEachTriggerData>
	{
		public override void Execute()
		{
			foreach (var entityId in Data.Entities.Value)
			{
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("foreach_entity_id", entityId);
					NotifyListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
			}
		}
	}
}
