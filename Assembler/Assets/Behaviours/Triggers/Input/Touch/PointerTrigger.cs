using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires on primary-pointer hover/press/hold/release and publishes the pointer's screen position plus
	/// the live camera ray (origin + direction) and the ray's intersection with a configurable world plane, so
	/// descriptors do screen→world picking without hand-rolled camera constants.</summary>
	/// <remarks>
	/// Reads the actual live output camera each time it fires, so picking stays correct when the camera moves —
	/// unlike an <c>!expr</c> that duplicates the camera transform as authored constants. With a moving camera,
	/// prefer <c>hover</c>/<c>hold</c> so the emitted world point tracks the camera; <c>press</c>/<c>release</c> sample once.
	/// Emit <c>origin</c>/<c>direction</c> to intersect any other plane or surface in a downstream expression.
	/// Properties:
	///   Phase [hover|press|hold|release]: When to fire — every frame the pointer has a position pressed or not (hover), once on press (press, default), every frame held (hold), or once on release (release).
	///   PlanePoint: A point on the world plane whose intersection becomes world_position. Defaults to (0, 0, 0).
	///   PlaneNormal: The world plane's normal. Defaults to (0, 1, 0) (the XZ ground plane); use (0, 0, 1) for a 2D XY game.
	/// Outputs:
	///   screen_position [Vector3]: Screen-space pointer position in pixels (z is 0).
	///   world_position [Vector3]: Where the camera ray through the pointer meets the configured plane (falls back to the ray origin when the ray is parallel to the plane).
	///   origin [Vector3]: World-space origin of the camera ray through the pointer.
	///   direction [Vector3]: Normalised world-space direction of the camera ray through the pointer.
	/// </remarks>
	public class PointerTrigger : InputTrigger<PointerTriggerData>
	{
		private bool _pressed;
		private UnityCamera? _camera;

		private void Update()
		{
			var pressed = Pointer.IsPressed;

			var fire = Data.Phase.ValueOr(PointerPhase.Press) switch
			{
				PointerPhase.Hover => true,
				PointerPhase.Hold => pressed,
				PointerPhase.Press => pressed && !_pressed,
				PointerPhase.Release => !pressed && _pressed,
				_ => false
			};

			if (fire)
			{
				Emit(Pointer.Position);
			}

			_pressed = pressed;
		}

		private void Emit(Vector3 screenPosition)
		{
			var camera = ResolveCamera();
			if (camera == null)
			{
				// No output camera yet — the screen position is still meaningful; world data is not.
				NotifyListeners(TriggerContext.New("screen_position", screenPosition));
				return;
			}

			var ray = camera.ScreenPointToRay(screenPosition);
			var world = IntersectPlane(ray, Data.PlanePoint.ValueOr(Vector3.zero), Data.PlaneNormal.ValueOr(Vector3.up));

			NotifyListeners(TriggerContext.New(b =>
			{
				b["screen_position"] = screenPosition;
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
