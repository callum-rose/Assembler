using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving
{
	public sealed class EntityTransformRegistry
	{
		private readonly Dictionary<string, Transform> _transforms = new();

		public void Register(string id, Transform transform)
		{
			if (_transforms.ContainsKey(id))
			{
				throw new System.InvalidOperationException(
					$"Entity id '{id}' is already registered. Entity ids must be unique across the game.");
			}

			_transforms[id] = transform;
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

		public bool TryGet(string id, out Transform? transform) =>
			_transforms.TryGetValue(id, out transform);
	}
}
