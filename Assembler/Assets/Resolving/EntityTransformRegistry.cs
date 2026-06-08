using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving
{
	public sealed class EntityTransformRegistry
	{
		private readonly Dictionary<string, Transform> _transforms = new();

		public void Register(string id, Transform transform)
		{
			if (!_transforms.TryAdd(id, transform))
			{
				throw new System.InvalidOperationException(
					$"Entity id '{id}' is already registered. Entity ids must be unique across the game.");
			}
		}

		public Transform Get(string id)
		{
			if (!_transforms.TryGetValue(id, out var transform))
			{
				throw new System.InvalidOperationException(
					$"No transform registered for entity id '{id}'. Available ids: {string.Join(", ", _transforms.Keys)}");
			}

			return transform;
		}
	}
}
