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

		// public string? TemplateId { get; set; }
		//
		// public string? EntityId { get; set; }

		public EntityVariableScope? VariableScope { get; set; }

		private void OnDestroy()
		{
			VariableScope?.Dispose();
			VariableScope = null;
		}
	}
}