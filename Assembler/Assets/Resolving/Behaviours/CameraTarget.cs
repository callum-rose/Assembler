using System;
using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Live <see cref="IValueProvider{T}"/> for a camera target's transform. Every read re-evaluates the
	/// backing delegate, so a tag target follows entities spawned after build and yields <c>null</c> while
	/// no matching entity exists (or once it is destroyed). Consumers null-check the result. The delegate
	/// wraps whatever registry the target needs, keeping this provider decoupled from those registries.
	/// </summary>
	public sealed class CameraTargetProvider : IValueProvider<Transform>
	{
		private readonly Func<Transform?> _resolve;

		public CameraTargetProvider(Func<Transform?> resolve) => _resolve = resolve;

		// May legitimately return null when the target entity is absent (not yet spawned / destroyed);
		// the camera reads this each frame and skips assigning a null transform. Suppression is justified.
		public Transform Get(TriggerContext ctx) => _resolve()!;

		object IValueProvider.Get(TriggerContext ctx) => Get(ctx);
	}

	public static class CameraTargetResolver
	{
		/// <summary>
		/// Resolve an info-layer <see cref="CameraTargetSource"/> into a live <see cref="IValueProvider{T}"/>
		/// of <see cref="Transform"/>. <paramref name="resolveByEntityTag"/> is supplied by the build factory
		/// as a closure over the live behaviour registry (mirroring how tagged listeners capture their query),
		/// so tag targets re-query live state and catch spawned entities. Absence resolves to
		/// <see cref="NullValueProvider{T}"/>.
		/// </summary>
		public static IValueProvider<Transform> Resolve(
			CameraTargetSource source,
			ResolutionContext ctx,
			Func<string, IReadOnlyList<Transform>> resolveByEntityTag)
		{
			switch (source)
			{
				case NoCameraTargetSource:
					return NullValueProvider<Transform>.Instance;

				case IdTarget id:
				{
					// Captured once: an id always names a build-time entity, registered before this runs.
					var transform = ctx.EntityTransforms.Get(id.Id);
					return new CameraTargetProvider(() => transform);
				}

				case TagTarget tag:
				{
					var tagProvider = tag.Tag.Resolve(ctx);
					return new CameraTargetProvider(() =>
					{
						var value = tagProvider.Get();
						if (string.IsNullOrEmpty(value))
						{
							return null;
						}

						var matches = resolveByEntityTag(value);
						return matches.Count > 0 ? matches[0] : null;
					});
				}

				default:
					throw new ArgumentException($"Unsupported camera target source '{source.GetType()}'");
			}
		}
	}
}
