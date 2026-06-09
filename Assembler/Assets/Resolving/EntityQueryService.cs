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
	/// Destroyed entities are skipped lazily: their <see cref="Transform"/> compares equal to null once Unity
	/// has torn them down, so they drop out of results without an explicit deregister step.
	///
	/// The implementation is a linear scan over the tag bucket; a spatial hash can replace it later behind this
	/// same interface without touching callers.
	/// </summary>
	public sealed class EntityQueryService
	{
		private readonly Dictionary<string, Transform> _byId = new();
		private readonly Dictionary<string, List<string>> _idsByTag = new();

		/// <summary>Adds an entity to the index under each of its tags. Call once per entity at instantiation.</summary>
		public void Register(string id, Transform transform, IReadOnlyList<string> tags)
		{
			_byId[id] = transform;

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

		/// <summary>Current world position of an entity, or <see cref="Vector3.zero"/> if unknown/destroyed.</summary>
		public Vector3 PositionOf(string id) =>
			_byId.TryGetValue(id, out var transform) && transform != null ? transform.position : Vector3.zero;

		/// <summary>
		/// Id of the nearest live entity carrying <paramref name="tag"/> within <paramref name="maxRange"/> of
		/// <paramref name="from"/>, or <c>null</c> if none. Pass <see cref="float.PositiveInfinity"/> for no limit.
		/// </summary>
		public string? Nearest(Vector3 from, string tag, float maxRange)
		{
			if (!_idsByTag.TryGetValue(tag, out var bucket))
			{
				return null;
			}

			string? best = null;
			var bestSqr = maxRange * maxRange;

			foreach (var id in bucket)
			{
				if (!TryLivePosition(id, out var position))
				{
					continue;
				}

				var sqr = (position - from).sqrMagnitude;

				// Strict '<' keeps the first (lowest-id) candidate on a tie, since the bucket is id-sorted.
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					best = id;
				}
			}

			return best;
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
			if (_byId.TryGetValue(id, out var transform) && transform != null)
			{
				position = transform.position;
				return true;
			}

			position = Vector3.zero;
			return false;
		}
	}
}
