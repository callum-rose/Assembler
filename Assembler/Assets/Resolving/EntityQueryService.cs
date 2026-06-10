using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving
{
	/// <summary>
	/// Tag-indexed spatial query service: nearest-with-tag, within-radius and within-cone lookups over the
	/// live set of entities. Populated as entities instantiate (alongside <see cref="EntityTransformRegistry"/>),
	/// it is the perception layer's read side — sensors and the <c>!query</c> tag resolve targets against it.
	///
	/// Queries iterate each tag bucket in id-sorted (ordinal) order and break ties toward the lowest id, so the
	/// result is deterministic given the same world (mirroring <c>BehaviourRegistry</c>'s stable ordering).
	///
	/// Entities deregister on destruction via <see cref="Unregister"/> (called from <c>GameEntity.OnDestroy</c>),
	/// so the index does not leak as entities churn. The destroyed-transform null check is a defensive backstop
	/// for the window between Unity tearing a transform down and that callback running.
	///
	/// The implementation is a linear scan over the tag bucket; a spatial hash can replace it later behind this
	/// same interface without touching callers.
	/// </summary>
	public sealed class EntityQueryService
	{
		private readonly Dictionary<string, Entry> _byId = new();
		private readonly Dictionary<string, List<string>> _idsByTag = new();

		/// <summary>Adds an entity to the index under each of its tags. Call once per entity at instantiation.</summary>
		public void Register(string id, Transform transform, IReadOnlyList<string> tags)
		{
			_byId[id] = new Entry(transform, tags);

			foreach (var tag in tags)
			{
				if (!_idsByTag.TryGetValue(tag, out var bucket))
				{
					bucket = new List<string>();
					_idsByTag[tag] = bucket;
				}

				// Keep buckets id-sorted so iteration order (and nearest tie-breaking) is deterministic.
				var index = bucket.BinarySearch(id, StringComparer.Ordinal);
				if (index < 0)
				{
					bucket.Insert(~index, id);
				}
			}
		}

		/// <summary>Removes an entity from the index. Safe to call for an unknown id (no-op).</summary>
		public void Unregister(string id)
		{
			if (!_byId.TryGetValue(id, out var entry))
			{
				return;
			}

			foreach (var tag in entry.Tags)
			{
				if (_idsByTag.TryGetValue(tag, out var bucket))
				{
					bucket.Remove(id);
				}
			}

			_byId.Remove(id);
		}

		/// <summary>Current world position of a live entity. Returns false if unknown or destroyed.</summary>
		public bool TryGetPosition(string id, out Vector3 position) => TryLivePosition(id, out position);

		/// <summary>
		/// Finds the nearest live entity carrying <paramref name="tag"/> within <paramref name="maxRange"/> of
		/// <paramref name="from"/>. Returns false (and an empty <paramref name="id"/>) if none. Pass
		/// <see cref="float.PositiveInfinity"/> for no range limit.
		/// </summary>
		public bool TryNearest(Vector3 from, string tag, float maxRange, out string id)
		{
			id = string.Empty;

			if (!_idsByTag.TryGetValue(tag, out var bucket))
			{
				return false;
			}

			var found = false;
			var bestSqr = maxRange * maxRange;

			foreach (var candidate in bucket)
			{
				if (!TryLivePosition(candidate, out var position))
				{
					continue;
				}

				var sqr = (position - from).sqrMagnitude;

				// Strict '<' keeps the first (lowest-id) candidate on a tie, since the bucket is id-sorted.
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					id = candidate;
					found = true;
				}
			}

			return found;
		}

		/// <summary>All live entities carrying <paramref name="tag"/> within <paramref name="radius"/>, id-sorted.</summary>
		public IReadOnlyList<string> WithinRadius(Vector3 from, string tag, float radius)
		{
			if (!_idsByTag.TryGetValue(tag, out var bucket))
			{
				return Array.Empty<string>();
			}

			var radiusSqr = radius * radius;
			var result = new List<string>();

			foreach (var id in bucket)
			{
				if (TryLivePosition(id, out var position) && (position - from).sqrMagnitude <= radiusSqr)
				{
					result.Add(id);
				}
			}

			return result;
		}

		/// <summary>
		/// All live entities carrying <paramref name="tag"/> within <paramref name="radius"/> and inside a cone
		/// of half-angle <paramref name="halfAngleDeg"/> about <paramref name="forward"/>, id-sorted.
		/// </summary>
		public IReadOnlyList<string> WithinCone(
			Vector3 from,
			Vector3 forward,
			string tag,
			float radius,
			float halfAngleDeg)
		{
			if (!_idsByTag.TryGetValue(tag, out var bucket))
			{
				return Array.Empty<string>();
			}

			var radiusSqr = radius * radius;
			var facing = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.right;
			var result = new List<string>();

			foreach (var id in bucket)
			{
				if (!TryLivePosition(id, out var position))
				{
					continue;
				}

				var offset = position - from;

				if (offset.sqrMagnitude > radiusSqr)
				{
					continue;
				}

				// A target at the cone origin has no direction; treat it as in-cone.
				if (offset.sqrMagnitude <= 1e-8f || Vector3.Angle(facing, offset) <= halfAngleDeg)
				{
					result.Add(id);
				}
			}

			return result;
		}

		private bool TryLivePosition(string id, out Vector3 position)
		{
			if (_byId.TryGetValue(id, out var entry) && entry.Transform != null)
			{
				position = entry.Transform.position;
				return true;
			}

			position = Vector3.zero;
			return false;
		}

		private readonly struct Entry
		{
			public Transform Transform { get; }
			public IReadOnlyList<string> Tags { get; }

			public Entry(Transform transform, IReadOnlyList<string> tags)
			{
				Transform = transform;
				Tags = tags;
			}
		}
	}
}
