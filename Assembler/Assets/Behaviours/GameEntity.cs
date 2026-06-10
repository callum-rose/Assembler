using System;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours
{
	public sealed class GameEntity : MonoBehaviour
	{
		[SerializeField] private string[] tags = Array.Empty<string>();

		public string[] Tags
		{
			get => tags;
			set => tags = value;
		}

		public EntityVariableScope? VariableScope { get; set; }

		/// <summary>The spatial-query index this entity registers into, so it can self-deregister on destruction.</summary>
		public EntityQueryService? Query { get; set; }

		// Self-registers into the spatial-query index here (and deregisters in OnDestroy) so the index's lifetime
		// is owned by the entity rather than the factory. The factory configures Query/Tags while the GameObject is
		// inactive, then activates it, so both are set by the time Awake runs.
		private void Awake()
		{
			Query?.Register(gameObject.name, transform, tags);
		}

		private void OnDestroy()
		{
			Query?.Unregister(gameObject.name);
			Query = null;

			VariableScope?.Dispose();
			VariableScope = null;
		}
	}
}
