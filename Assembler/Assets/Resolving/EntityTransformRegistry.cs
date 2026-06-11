using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving
{
	/// <summary>
	/// Maps each entity id to its live <see cref="Transform"/>, the read/write target behind <c>!entity</c> and
	/// <c>!rigidbody</c> property references. Populated as entities instantiate (alongside
	/// <see cref="EntityQueryService"/>).
	///
	/// Entities deregister on destruction via <see cref="Unregister"/> (called from <c>GameEntity.OnDestroy</c>),
	/// so the registry does not leak as entities churn. <see cref="Get"/> additionally treats a destroyed
	/// transform as absent — a defensive backstop for the window between Unity tearing a transform down and that
	/// callback running, so a reference to a despawned entity fails with a clear, id-naming error here rather than
	/// a <see cref="MissingReferenceException"/> surfacing deep inside a property provider's read.
	/// </summary>
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

		/// <summary>Removes an entity from the registry. Safe to call for an unknown id (no-op).</summary>
		public void Unregister(string id) => _transforms.Remove(id);

		public Transform Get(string id)
		{
			// The '!= null' check uses Unity's overloaded operator, so a destroyed-but-not-yet-deregistered
			// transform is treated the same as a missing one: a clear error naming the id, not a later
			// MissingReferenceException from reading a torn-down transform.
			if (_transforms.TryGetValue(id, out var transform) && transform != null)
			{
				return transform;
			}

			throw new System.InvalidOperationException(
				$"No live transform registered for entity id '{id}'. Available ids: {string.Join(", ", _transforms.Keys)}");
		}
	}
}
