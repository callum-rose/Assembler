using System;
using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Runtime-resolved camera target. Id targets resolve a single transform captured from the build-time
	/// entity-transform registry; tag targets re-query the live behaviour registry on every read, so they
	/// pick up entities spawned after build. A tag can match several entities (used by <c>camera group</c>).
	/// Cinemachine-free so it can live in the resolving layer alongside the camera <c>*Data</c> classes.
	/// </summary>
	public interface ICameraTarget
	{
		/// <summary>The first matching transform, or false when none currently exist.</summary>
		bool TryGetTransform(out Transform transform);

		/// <summary>All currently matching transforms (empty when none).</summary>
		IReadOnlyList<Transform> GetTransforms();
	}

	/// <summary>An <c>{ Id: … }</c> target — one entity, resolved (and cached) from the transform registry.</summary>
	public sealed class IdCameraTarget : ICameraTarget
	{
		private readonly EntityTransformRegistry _transforms;
		private readonly string _id;
		private Transform? _cached;

		public IdCameraTarget(EntityTransformRegistry transforms, string id) =>
			(_transforms, _id) = (transforms, id);

		public bool TryGetTransform(out Transform transform)
		{
			_cached ??= _transforms.Get(_id);
			transform = _cached;
			return transform != null;
		}

		public IReadOnlyList<Transform> GetTransforms() =>
			TryGetTransform(out var t) ? new[] { t } : Array.Empty<Transform>();
	}

	/// <summary>A <c>{ Tag: … }</c> target — every entity carrying the tag, re-queried each read (catches spawns).</summary>
	public sealed class TagCameraTarget : ICameraTarget
	{
		private readonly IValueProvider<string> _tag;
		private readonly Func<string, IReadOnlyList<Transform>> _resolveByTag;

		public TagCameraTarget(IValueProvider<string> tag, Func<string, IReadOnlyList<Transform>> resolveByTag) =>
			(_tag, _resolveByTag) = (tag, resolveByTag);

		public IReadOnlyList<Transform> GetTransforms()
		{
			var tag = _tag.Get();
			return string.IsNullOrEmpty(tag) ? Array.Empty<Transform>() : _resolveByTag(tag);
		}

		public bool TryGetTransform(out Transform transform)
		{
			var transforms = GetTransforms();
			if (transforms.Count > 0)
			{
				transform = transforms[0];
				return transform != null;
			}

			transform = null!;
			return false;
		}
	}

	public static class CameraTargetResolver
	{
		/// <summary>
		/// Resolve an info-layer <see cref="CameraTargetSource"/> into a runtime <see cref="ICameraTarget"/>.
		/// <paramref name="resolveByEntityTag"/> is supplied by the build factory as a closure over the live
		/// behaviour registry (mirrors how tagged listeners capture their registry query), so tag targets
		/// re-query live state and catch spawned entities.
		/// </summary>
		public static ICameraTarget Resolve(
			CameraTargetSource source,
			ResolutionContext ctx,
			Func<string, IReadOnlyList<Transform>> resolveByEntityTag) =>
			source switch
			{
				IdTarget id => new IdCameraTarget(ctx.EntityTransforms, id.Id),
				TagTarget tag => new TagCameraTarget(tag.Tag.Resolve(ctx), resolveByEntityTag),
				_ => throw new ArgumentException($"Unsupported camera target source '{source.GetType()}'")
			};
	}
}
