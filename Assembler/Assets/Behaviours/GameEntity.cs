using System;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours
{
	public sealed class GameEntity : MonoBehaviour
	{
		[SerializeField] private string[] tags = Array.Empty<string>();

		/// <summary>The descriptor id of this entity — the authoritative key it is indexed under (matches the
		/// entity id half of a <c>BehaviourDescriptor</c>). The factory sets this; behaviours that need their own
		/// entity id read it here rather than relying on <c>gameObject.name</c>.</summary>
		public string Id { get; set; } = string.Empty;

		/// <summary>The template this entity was spawned from, or <c>null</c> for hand-authored / placement /
		/// game-controller entities that never came through <c>Spawn</c>. Stamped only on pooled spawns; it is the
		/// key the entity is returned to the pool under, and the flag that distinguishes a pool-return from a real
		/// destroy (see <c>GameEntityFactory.Despawn</c>). Child entities of a pooled tree stay <c>null</c> — they
		/// are pooled as part of the root, never independently.</summary>
		public string? TemplateId { get; set; }

		public string[] Tags
		{
			get => tags;
			set => tags = value;
		}

		public EntityVariableScope? VariableScope { get; set; }

		/// <summary>The spatial-query index this entity self-registers into on <see cref="Activate"/>.</summary>
		public EntityQueryService? Query { get; set; }

		/// <summary>Raised from <see cref="OnDestroy"/>. The factory subscribes each runtime index's deregistration
		/// (query / transform / behaviour) so the entity self-evicts from all of them on destruction without holding a
		/// reference to each — routing them through one event also crosses the assembly boundary to the higher-level
		/// <c>BehaviourRegistry</c> that this type can't reference directly. Subscribers capture the entity id they need.</summary>
		public event Action? Destroying;

		// Self-registers into the spatial-query index. Called explicitly by the factory after the entity is
		// configured and its GameObject activated — NOT from Awake, because Awake runs only once per component
		// lifetime: a pooled shell keeps its GameEntity, so its Awake would not re-fire on reuse and the entity
		// would silently never re-register. Activate is re-run on every (re)build, so reuse re-registers correctly.
		public void Activate()
		{
			Query?.Register(Id, transform, tags);
		}

		/// <summary>Runs the destruction teardown — fire <see cref="Destroying"/> so the entity self-evicts from every
		/// runtime index — WITHOUT destroying the GameObject, so the shell can be returned to the pool and rebuilt.
		/// Called by <c>GameEntityFactory.Despawn</c> for a pooled entity; the next build re-subscribes
		/// <see cref="Destroying"/> and installs a fresh scope.</summary>
		public void Recycle() => TearDown();

		private void OnDestroy() => TearDown();

		private void TearDown()
		{
			Destroying?.Invoke();
			Destroying = null;
			Query = null;

			VariableScope?.Dispose();
			VariableScope = null;
		}
	}
}
