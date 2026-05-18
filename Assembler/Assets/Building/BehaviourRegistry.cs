using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Parsing.Info;

namespace Assembler.Building
{
	public interface IReadOnlyBehaviourRegistry
	{
		GameBehaviour this[BehaviourDescriptor descriptor] { get; }
	}

	public static class BehaviourRegistryExtensions
	{
		public static void Register(this BehaviourRegistry registry, EntityBuildResult result)
		{
			foreach (var (descriptor, behaviour) in result.Behaviours)
			{
				registry.Register(descriptor, behaviour);
			}
		}
	}

	public class BehaviourRegistry : IReadOnlyBehaviourRegistry
	{
		public GameBehaviour this[BehaviourDescriptor descriptor] => _behaviours[descriptor];
		
		private readonly Dictionary<BehaviourDescriptor, GameBehaviour> _behaviours = new();

		public void Register(BehaviourDescriptor descriptor, GameBehaviour behaviour)
		{
			_behaviours.Add(descriptor, behaviour);
		}
	}
}