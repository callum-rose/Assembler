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

		/// <summary>The spatial-query index this entity registers into, so it can self-deregister on destruction.</summary>
		public EntityQueryService? Query { get; set; }

		/// <summary>The transform index this entity is registered in, so it can self-deregister on destruction.</summary>
		public EntityTransformRegistry? Transforms { get; set; }

		/// <summary>Evicts this entity's behaviours from the runtime <c>BehaviourRegistry</c> on destruction. The registry
		/// lives in a higher assembly than this type, so the factory wires it as a callback rather than a direct reference.</summary>
		public Action<string>? DeregisterBehaviours { get; set; }

		// Self-registers into the spatial-query index here (and deregisters in OnDestroy) so the index's lifetime
		// is owned by the entity rather than the factory. The factory configures Query/Tags/Id while the GameObject
		// is inactive, then activates it, so all are set by the time Awake runs.
		private void Awake()
		{
			Query?.Register(Id, transform, tags);
		}

		private void OnDestroy()
		{
			Query?.Unregister(Id);
			Query = null;

			Transforms?.Unregister(Id);
			Transforms = null;

			DeregisterBehaviours?.Invoke(Id);
			DeregisterBehaviours = null;

			VariableScope?.Dispose();
			VariableScope = null;
		}
	}
}
