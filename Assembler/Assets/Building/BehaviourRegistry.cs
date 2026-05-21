using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Parsing.Info;

namespace Assembler.Building
{
	public interface IReadOnlyBehaviourRegistry
	{
		GameBehaviour this[BehaviourDescriptor descriptor] { get; }
		IReadOnlyList<GameBehaviour> GetByBehaviourTag(string behaviourTag, string? entityTag = null);
		IReadOnlyList<GameBehaviour> GetByEntityTagAndBehaviourId(string entityTag, string behaviourId);
	}

	public static class BehaviourRegistryExtensions
	{
		public static void Register(this BehaviourRegistry registry, EntityBuildResult result)
		{
			foreach (var (descriptor, behaviour, behaviourTags) in result.Behaviours)
			{
				registry.Register(descriptor, behaviour, behaviourTags);
			}
		}
	}

	public class BehaviourRegistry : IReadOnlyBehaviourRegistry
	{
		public GameBehaviour this[BehaviourDescriptor descriptor] => _behaviours[descriptor];

		private readonly Dictionary<BehaviourDescriptor, GameBehaviour> _behaviours = new();
		private readonly Dictionary<string, List<GameBehaviour>> _behavioursByTag = new();

		public void Register(BehaviourDescriptor descriptor, GameBehaviour behaviour, IReadOnlyList<string> behaviourTags)
		{ 
			_behaviours.Add(descriptor, behaviour);

			foreach (var tag in behaviourTags)
			{
				if (!_behavioursByTag.TryGetValue(tag, out var list))
				{
					_behavioursByTag[tag] = list = new List<GameBehaviour>();
				}

				list.Add(behaviour);
			}
		}

		public void Unregister(string entityId)
		{
			var toRemove = _behaviours.Keys.Where(k => k.EntityId == entityId).ToArray();
			var removedBehaviours = new HashSet<GameBehaviour>();
			foreach (var key in toRemove)
			{
				removedBehaviours.Add(_behaviours[key]);
				_behaviours.Remove(key);
			}

			foreach (var list in _behavioursByTag.Values)
			{
				list.RemoveAll(removedBehaviours.Contains);
			}
		}

		public IReadOnlyList<GameBehaviour> GetByBehaviourTag(string behaviourTag, string? entityTag = null)
		{
			if (!_behavioursByTag.TryGetValue(behaviourTag, out var list))
			{
				return Array.Empty<GameBehaviour>();
			}

			return entityTag == null
				? list.Where(b => b).ToArray()
				: list.Where(b => b && b.gameObject.GetComponent<GameEntity>()?.Tags.Contains(entityTag) == true).ToArray();
		}

		public IReadOnlyList<GameBehaviour> GetByEntityTagAndBehaviourId(string entityTag, string behaviourId)
		{
			return _behaviours
				.Where(kv => kv.Key.BehaviourId == behaviourId
				             && kv.Value
				             // TODO Null coalescing may be an issue
				             && kv.Value.gameObject.GetComponent<GameEntity>()?.Tags.Contains(entityTag) == true)
				.Select(kv => kv.Value)
				.ToArray();
		}
	}
}