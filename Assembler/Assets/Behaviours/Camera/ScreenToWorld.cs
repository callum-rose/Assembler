using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Unprojects a screen-space position through the live output camera and emits the world point on a
	/// configurable plane plus the full camera ray, so descriptors do screen→world picking without hand-rolled
	/// camera constants. Insert it in a chain after an input behaviour that supplies the screen position.</summary>
	/// <remarks>
	/// Not a self-firing trigger — it does nothing until executed by an upstream input behaviour (e.g. an
	/// <c>input action</c>, <c>tap trigger</c>, or <c>drag trigger</c>) whose screen-position output is bound to
	/// ScreenPosition. It reads the actual live camera each time it runs, so picking stays correct when the camera
	/// moves (no pinned vcam, no authored constants). Emit <c>origin</c>/<c>direction</c> to intersect any other
	/// plane or surface in a downstream expression.
	/// Properties:
	///   ScreenPosition: Screen-space pixel position to unproject (z ignored). Bind to an upstream output, e.g. `!output axis` from an `input action` or `!output position` from a `tap trigger`.
	///   PlanePoint: A point on the world plane whose intersection becomes world_position. Defaults to (0, 0, 0).
	///   PlaneNormal: The world plane's normal. Defaults to (0, 1, 0) (the XZ ground plane); use (0, 0, 1) for a 2D XY game.
	/// Outputs:
	///   screen_position [Vector3]: The screen-space position that was unprojected (z is 0).
	///   world_position [Vector3]: Where the camera ray through the screen point meets the configured plane (falls back to the ray origin when the ray is parallel to the plane).
	///   origin [Vector3]: World-space origin of the camera ray through the screen point.
	///   direction [Vector3]: Normalised world-space direction of the camera ray through the screen point.
	/// </remarks>
	public sealed class ScreenToWorld : Trigger<ScreenToWorldData>, IAmExecutable
	{
		private UnityCamera? _camera;

		public void Execute(TriggerContext ctx)
		{
			var screen = Data.ScreenPosition.Get(ctx);
			var camera = ResolveCamera();
			if (camera == null)
			{
				// No output camera yet — pass the screen position through; world data is unavailable.
				NotifyListeners(ctx.With("screen_position", screen));
				return;
			}

			var ray = camera.ScreenPointToRay(screen);
			var world = IntersectPlane(ray, Data.PlanePoint.ValueOr(ctx, Vector3.zero), Data.PlaneNormal.ValueOr(ctx, Vector3.up));

			NotifyListeners(ctx.With(b =>
			{
				b["screen_position"] = screen;
				b["world_position"] = world;
				b["origin"] = ray.origin;
				b["direction"] = ray.direction;
			}));
		}

		// Intersects the ray with the plane (point + normal). Falls back to the ray origin when the ray runs
		// parallel to the plane (no unique intersection), so world_position is always defined.
		private static Vector3 IntersectPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal)
		{
			var denominator = Vector3.Dot(ray.direction, planeNormal);
			if (Mathf.Abs(denominator) < 1e-6f)
			{
				return ray.origin;
			}

			var distance = Vector3.Dot(planePoint - ray.origin, planeNormal) / denominator;
			return ray.origin + ray.direction * distance;
		}

		// The output camera is created once by the `camera` behaviour (a real Camera + Cinemachine brain) and
		// then persists, so resolve it lazily and cache it. Nothing tags a MainCamera, so fall back to the first
		// enabled camera. `!= null` (not `is not null`) re-resolves if a cached camera is later destroyed.
		private UnityCamera? ResolveCamera()
		{
			if (_camera != null)
			{
				return _camera;
			}

			_camera = UnityCamera.main;
			if (_camera == null && UnityCamera.allCamerasCount > 0)
			{
				_camera = UnityCamera.allCameras[0];
			}

			return _camera;
		}
	}
}
