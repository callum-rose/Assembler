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
		IReadOnlyList<GameBehaviour> GetByEntityTag(string entityTag);
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

		/// <summary>All registered behaviours keyed by descriptor. Used by debug tooling to enumerate the live graph.</summary>
		public IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour> All => _behaviours;

		private readonly Dictionary<BehaviourDescriptor, GameBehaviour> _behaviours = new();
		private readonly Dictionary<string, List<GameBehaviour>> _behavioursByTag = new();

		/// <summary>
		/// Entity-tag index: each entity tag maps to the behaviours carried by entities with that tag, so tagged-listener
		/// notifies (<see cref="GetByEntityTag"/> / <see cref="GetByEntityTagAndBehaviourId"/>) are O(matches) rather than
		/// a LINQ scan of every behaviour ever created with a <c>GetComponent&lt;GameEntity&gt;()</c> per entry. Entity tags
		/// are snapshotted at registration (mirroring how the pre-index code read them from the live component).
		/// </summary>
		private readonly Dictionary<string, List<EntityTagEntry>> _behavioursByEntityTag = new();

		/// <summary>Descriptors grouped by entity id, so <see cref="DeregisterEntity"/> can evict an entity's behaviours
		/// without scanning the whole registry.</summary>
		private readonly Dictionary<string, List<BehaviourDescriptor>> _descriptorsByEntityId = new();

		/// <summary>Per-descriptor metadata needed to reverse a registration on deregistration (which tag buckets the
		/// behaviour was indexed into).</summary>
		private readonly Dictionary<BehaviourDescriptor, Registration> _registrations = new();

		/// <summary>
		/// Registration order of each behaviour. Registration runs in stable entity/behaviour list order, so this
		/// gives a deterministic ordering for queries that would otherwise iterate the unordered <see cref="_behaviours"/>
		/// dictionary (see <see cref="GetByEntityTagAndBehaviourId"/>). Part of the Level 1 determinism guarantee.
		/// </summary>
		private readonly Dictionary<GameBehaviour, int> _registrationIndex = new();
		private int _nextIndex;

		public void Register(BehaviourDescriptor descriptor, GameBehaviour behaviour, IReadOnlyList<string>? behaviourTags = null)
		{
			_behaviours.Add(descriptor, behaviour);
			_registrationIndex[behaviour] = _nextIndex++;

			// Entity tags are read once here (the entity component is configured before registration) rather than via a
			// GetComponent per query, and remembered so deregistration can find the buckets to evict from.
			var entityTags = behaviour.gameObject.GetComponent<GameEntity>()?.Tags ?? Array.Empty<string>();
			_registrations[descriptor] = new Registration(behaviour, behaviourTags ?? Array.Empty<string>(), entityTags);

			if (!_descriptorsByEntityId.TryGetValue(descriptor.EntityId, out var entityDescriptors))
			{
				_descriptorsByEntityId[descriptor.EntityId] = entityDescriptors = new List<BehaviourDescriptor>();
			}

			entityDescriptors.Add(descriptor);

			foreach (var tag in entityTags)
			{
				if (!_behavioursByEntityTag.TryGetValue(tag, out var entityTagBucket))
				{
					_behavioursByEntityTag[tag] = entityTagBucket = new List<EntityTagEntry>();
				}

				entityTagBucket.Add(new EntityTagEntry(behaviour, descriptor.BehaviourId));
			}

			foreach (var tag in behaviourTags ?? Array.Empty<string>())
			{
				if (!_behavioursByTag.TryGetValue(tag, out var list))
				{
					_behavioursByTag[tag] = list = new List<GameBehaviour>();
				}

				list.Add(behaviour);
			}
		}

		/// <summary>
		/// Evicts every behaviour belonging to <paramref name="entityId"/> from all indexes. Called from
		/// <c>GameEntity.OnDestroy</c> so spawn/destroy churn doesn't leak the registry (mirroring
		/// <c>EntityQueryService.Unregister</c>). Safe to call for an unknown id (no-op).
		/// </summary>
		public void DeregisterEntity(string entityId)
		{
			if (!_descriptorsByEntityId.TryGetValue(entityId, out var descriptors))
			{
				return;
			}

			foreach (var descriptor in descriptors)
			{
				Deregister(descriptor);
			}

			_descriptorsByEntityId.Remove(entityId);
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
			if (!_behavioursByEntityTag.TryGetValue(entityTag, out var bucket))
			{
				return Array.Empty<GameBehaviour>();
			}

			return bucket
				.Where(e => e.BehaviourId == behaviourId && e.Behaviour)
				.Select(e => e.Behaviour)
				.OrderBy(b => _registrationIndex[b])
				.ToArray();
		}

		public IReadOnlyList<GameBehaviour> GetByEntityTag(string entityTag)
		{
			if (!_behavioursByEntityTag.TryGetValue(entityTag, out var bucket))
			{
				return Array.Empty<GameBehaviour>();
			}

			return bucket
				.Where(e => e.Behaviour)
				.Select(e => e.Behaviour)
				.OrderBy(b => _registrationIndex[b])
				.ToArray();
		}

		private void Deregister(BehaviourDescriptor descriptor)
		{
			if (!_registrations.TryGetValue(descriptor, out var registration))
			{
				return;
			}

			var behaviour = registration.Behaviour;

			_behaviours.Remove(descriptor);
			_registrationIndex.Remove(behaviour);
			_registrations.Remove(descriptor);

			foreach (var tag in registration.BehaviourTags)
			{
				if (_behavioursByTag.TryGetValue(tag, out var list))
				{
					list.Remove(behaviour);
				}
			}

			foreach (var tag in registration.EntityTags)
			{
				if (_behavioursByEntityTag.TryGetValue(tag, out var bucket))
				{
					bucket.RemoveAll(e => e.Behaviour == behaviour);
				}
			}
		}

		private readonly struct EntityTagEntry
		{
			public GameBehaviour Behaviour { get; }
			public string BehaviourId { get; }

			public EntityTagEntry(GameBehaviour behaviour, string behaviourId)
			{
				Behaviour = behaviour;
				BehaviourId = behaviourId;
			}
		}

		private readonly struct Registration
		{
			public GameBehaviour Behaviour { get; }
			public IReadOnlyList<string> BehaviourTags { get; }
			public IReadOnlyList<string> EntityTags { get; }

			public Registration(GameBehaviour behaviour, IReadOnlyList<string> behaviourTags, IReadOnlyList<string> entityTags)
			{
				Behaviour = behaviour;
				BehaviourTags = behaviourTags;
				EntityTags = entityTags;
			}
		}
	}
}
