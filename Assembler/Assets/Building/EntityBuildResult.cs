using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Parsing.Info;

namespace Assembler.Building
{
	public class EntityBuildResult
	{
		public IReadOnlyList<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)> Behaviours { get; }
		public IReadOnlyList<InitialiseBehaviourEvent> Initialisations { get; }

		public EntityBuildResult(
			IReadOnlyList<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)> behaviours,
			IReadOnlyList<InitialiseBehaviourEvent> initialisations)
		{
			Behaviours = behaviours;
			Initialisations = initialisations;
		}
	}
}
