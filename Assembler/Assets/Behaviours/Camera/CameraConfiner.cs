using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds a Cinemachine confiner extension that clamps the virtual camera so it never leaves a bounding
	/// volume defined by another entity's collider. <c>Mode</c> picks a 2D <c>Collider2D</c> boundary or a 3D
	/// <c>Collider</c> volume.</summary>
	/// <remarks>
	/// This is a <b>modifier</b> behaviour: it needs a virtual camera (<c>camera follow</c>/<c>camera orbit</c>/
	/// <c>camera group</c>) on the same entity and must be listed <b>after</b> it, or initialisation throws. The
	/// bounding collider is read from the <c>Bounds</c> entity and re-resolved each frame until found, so a tag
	/// target works even if the bounds entity spawns after build. Confining runs on real frame time
	/// (presentation-only). 2D mode confines against a <c>Collider2D</c>; 3D mode against a <c>Collider</c>.
	/// Properties:
	///   Bounds [Tag/Id]: Entity whose collider defines the boundary, as { Tag: … } or { Id: … }. 2D mode reads its Collider2D, 3D mode its Collider.
	///   Mode: "2d" (clamp to a Collider2D, default) or "3d" (clamp to a Collider volume).
	///   Damping: 2D only — how softly the camera is pushed back inside the bounds, in seconds (default 0 = instant).
	///   Padding: Distance from the edge at which the camera starts slowing before the hard boundary (default 0).
	/// </remarks>
	public sealed class CameraConfiner : GameBehaviour<CameraConfinerData>
	{
		private CinemachineConfiner2D? _confiner2D;
		private CinemachineConfiner3D? _confiner3D;
		private bool _boundsAssigned;

		protected override void OnInitialise(CameraConfinerData data)
		{
			CameraModifier.RequireVirtualCamera(gameObject, "camera confiner");

			if (data.Mode.ValueOr(CameraConfinerMode.TwoD) == CameraConfinerMode.ThreeD)
			{
				_confiner3D = gameObject.AddComponent<CinemachineConfiner3D>();
				data.Padding.UseIfValueExists(p => _confiner3D.SlowingDistance = p);
			}
			else
			{
				_confiner2D = gameObject.AddComponent<CinemachineConfiner2D>();
				data.Damping.UseIfValueExists(d => _confiner2D.Damping = d);
				data.Padding.UseIfValueExists(p => _confiner2D.SlowingDistance = p);
			}
		}

		// The bounding collider lives on another entity, which may not exist at build time (tag target). Resolve it
		// lazily each frame until found, assign it once, then stop looking.
		private void Update()
		{
			if (_boundsAssigned || Data.Bounds is NullValueProvider<Transform>)
			{
				return;
			}

			var bounds = Data.Bounds.Get();
			if (bounds == null)
			{
				return;
			}

			if (_confiner3D != null)
			{
				var volume = bounds.GetComponent<Collider>();
				if (volume != null)
				{
					_confiner3D.BoundingVolume = volume;
					_boundsAssigned = true;
				}
			}
			else if (_confiner2D != null)
			{
				var shape = bounds.GetComponent<Collider2D>();
				if (shape != null)
				{
					_confiner2D.BoundingShape2D = shape;
					_confiner2D.InvalidateBoundingShapeCache();
					_boundsAssigned = true;
				}
			}
		}
	}
}
