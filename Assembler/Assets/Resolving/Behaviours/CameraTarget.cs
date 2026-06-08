using System;
using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Runtime-resolved camera target. Id targets resolve a single transform captured from the build-time
	/// entity-transform registry; tag targets re-query the live behaviour registry on every read, so they
	/// pick up entities spawned after build. Cinemachine-free so it can live in the resolving layer
	/// alongside the camera <c>*Data</c> classes.
	/// </summary>
	public interface ICameraTarget
	{
		/// <summary>The current target transform, or false when none currently exists.</summary>
		bool TryGetTransform(out Transform transform);
	}

	/// <summary>The absence of a target — never resolves a transform.</summary>
	public sealed class NoCameraTarget : ICameraTarget
	{
		public readonly static NoCameraTarget Instance = new();

		public bool TryGetTransform(out Transform transform)
		{
			transform = null!;
			return false;
		}
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
	}

	/// <summary>A <c>{ Tag: … }</c> target — re-queried each read so it catches entities spawned after build.</summary>
	public sealed class TagCameraTarget : ICameraTarget
	{
		private readonly IValueProvider<string> _tag;
		private readonly Func<string, IReadOnlyList<Transform>> _resolveByTag;

		public TagCameraTarget(IValueProvider<string> tag, Func<string, IReadOnlyList<Transform>> resolveByTag) =>
			(_tag, _resolveByTag) = (tag, resolveByTag);

		public bool TryGetTransform(out Transform transform)
		{
			var tag = _tag.Get();
			var matches = string.IsNullOrEmpty(tag) ? Array.Empty<Transform>() : _resolveByTag(tag);
			if (matches.Count > 0 && matches[0] != null)
			{
				transform = matches[0];
				return true;
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
				NoCameraTargetSource => NoCameraTarget.Instance,
				IdTarget id => new IdCameraTarget(ctx.EntityTransforms, id.Id),
				TagTarget tag => new TagCameraTarget(tag.Tag.Resolve(ctx), resolveByEntityTag),
				_ => throw new ArgumentException($"Unsupported camera target source '{source.GetType()}'")
			};
	}
}
