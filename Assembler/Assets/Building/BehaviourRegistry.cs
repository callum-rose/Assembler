using System.Collections.Generic;
using Assembler.Core;
using Assembler.Parsing.Info;

namespace Assembler.Building
{
	public class BehaviourRegistry
	{
		private readonly Dictionary<BehaviourDescriptor, GameBehaviour> _registry = new();

		public void Register(BehaviourDescriptor behaviour, GameBehaviour gameBehaviour) =>
			_registry[behaviour] = gameBehaviour;

		public GameBehaviour Get(BehaviourDescriptor behaviour) => _registry[behaviour];
	}
}