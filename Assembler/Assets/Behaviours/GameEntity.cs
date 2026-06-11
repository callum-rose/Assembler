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

		public string[] Tags
		{
			get => tags;
			set => tags = value;
		}

		public EntityVariableScope? VariableScope { get; set; }

		/// <summary>The spatial-query index this entity self-registers into on <see cref="Awake"/>.</summary>
		public EntityQueryService? Query { get; set; }

		/// <summary>Raised from <see cref="OnDestroy"/>. The factory subscribes each runtime index's deregistration
		/// (query / transform / behaviour) so the entity self-evicts from all of them on destruction without holding a
		/// reference to each — routing them through one event also crosses the assembly boundary to the higher-level
		/// <c>BehaviourRegistry</c> that this type can't reference directly. Subscribers capture the entity id they need.</summary>
		public event Action? Destroying;

		// Self-registers into the spatial-query index here so the index's lifetime is owned by the entity rather than
		// the factory. The factory configures Query/Tags/Id while the GameObject is inactive, then activates it, so all
		// are set by the time Awake runs.
		private void Awake()
		{
			Query?.Register(Id, transform, tags);
		}

		private void OnDestroy()
		{
			Destroying?.Invoke();
			Destroying = null;
			Query = null;

			VariableScope?.Dispose();
			VariableScope = null;
		}
	}
}
