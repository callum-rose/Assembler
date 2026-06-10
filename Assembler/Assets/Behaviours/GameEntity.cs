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

		/// <summary>The spatial-query index this entity is registered in, so it can deregister on destruction.</summary>
		public EntityQueryService? Query { get; set; }

		private void OnDestroy()
		{
			Query?.Unregister(gameObject.name);
			Query = null;

			VariableScope?.Dispose();
			VariableScope = null;
		}
	}
}
