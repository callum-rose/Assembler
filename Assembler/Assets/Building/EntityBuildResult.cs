using System.Collections.Generic;
using Assembler.Core;
using Assembler.Parsing.Info;

namespace Assembler.Building
{
	public class EntityBuildResult
	{
		public IReadOnlyList<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour)> Behaviours { get; }
		public IReadOnlyList<InitialiseBehaviourEvent> Initialisations { get; }

		public EntityBuildResult(
			IReadOnlyList<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour)> behaviours,
			IReadOnlyList<InitialiseBehaviourEvent> initialisations)
		{
			Behaviours = behaviours;
			Initialisations = initialisations;
		}
	}
}
