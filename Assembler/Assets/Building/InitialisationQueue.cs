using System.Collections.Generic;

namespace Assembler.Building
{
	public class InitialisationQueue
	{
		private readonly List<InitialiseBehaviourEvent> _actions = new();

		public void Enqueue(EntityBuildResult result)
		{
			_actions.AddRange(result.Initialisations);
		}

		public void ExecuteAll(BehaviourRegistry registry)
		{
			foreach (var action in _actions)
			{
				action(registry);
			}
		}
	}
}
