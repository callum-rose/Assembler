using System;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds a Cinemachine virtual camera that follows and/or looks at a target entity, blended by the brain on
	/// the output <c>camera</c>. <c>Mode</c> picks a 2D screen-space rig or a 3D world-offset rig; omit <c>Target</c>
	/// for a pure look-at camera.</summary>
	/// <remarks>
	/// The follow/look-at targets are re-resolved every frame, so tag targets track entities spawned after build.
	/// Damping and blending run on real frame time (presentation-only) and never feed back into game logic.
	/// Properties:
	///   Target [Tag/Id]: Entity to follow, as { Tag: &lt;entity-tag&gt; } or { Id: &lt;entity-id&gt; }. Omit for look-at only.
	///   LookAt [Tag/Id]: Entity to aim at, as { Tag: … } or { Id: … }. Adds an aim composer.
	///   Mode: "2d" (screen-space framing, default) or "3d" (world-space follow offset + aim).
	///   Priority: Virtual-camera priority; the brain shows the highest-priority live vcam.
	///   Lens: Orthographic size (2D) or field of view in degrees (3D), depending on the output camera projection.
	///   Damping: How softly the camera follows (seconds-ish); 0 is instant. Applies to body and aim.
	///   DeadZone: 2D only — size (0..1 of the screen) of the region the target can move in without the camera reacting.
	///   CameraDistance: 2D only — distance the camera keeps in front of the target along its view axis (default 10). Must be &gt; 0 or an orthographic camera sits on the target's plane and sees nothing.
	///   ScreenOffset [Vector3]: 2D only — where on screen the target sits, as an offset from centre (-0.5..0.5); z is ignored.
	///   FollowOffset [Vector3]: 3D only — world-space offset the camera maintains from the target.
	/// </remarks>
	public class CameraFollow : GameBehaviour<CameraFollowData>
	{
		private CinemachineCamera _cam = null!;

		protected override void OnInitialise(CameraFollowData data)
		{
			_cam = gameObject.AddComponent<CinemachineCamera>();
			data.Priority.UseIfValueExists(p => _cam.Priority = p);
			data.Lens.UseIfValueExists(SetLens);

			if (data.Mode.ValueOr(CameraFollowMode.TwoD) == CameraFollowMode.ThreeD)
			{
				AddFollowBody(data);
			}
			else
			{
				AddScreenComposer(data);
			}

			if (data.LookAt is not NullValueProvider<Transform>)
			{
				AddAim(data);
			}
		}

		// 3D rig: maintain a world-space offset from the target.
		private void AddFollowBody(CameraFollowData data)
		{
			var follow = gameObject.AddComponent<CinemachineFollow>();
			data.FollowOffset.UseIfValueExists(o => follow.FollowOffset = o);
			data.Damping.UseIfValueExists(d =>
			{
				var tracker = follow.TrackerSettings;
				tracker.PositionDamping = new Vector3(d, d, d);
				follow.TrackerSettings = tracker;
			});
		}

		// 2D rig: frame the target in screen space, keeping a distance in front of it.
		private void AddScreenComposer(CameraFollowData data)
		{
			var composer = gameObject.AddComponent<CinemachinePositionComposer>();

			// Cinemachine only sets CameraDistance's default in Reset() (editor-only), so a runtime-added
			// composer starts at 0 — which puts an orthographic camera on the target's z-plane where it
			// can't see it. Default to 10 (overridable) so the camera always stays in front of the target.
			composer.CameraDistance = data.CameraDistance.ValueOr(10f);
			data.Damping.UseIfValueExists(d => composer.Damping = new Vector3(d, d, d));

			var composition = composer.Composition;
			data.ScreenOffset.UseIfValueExists(o => composition.ScreenPosition = (Vector2)o);
			data.DeadZone.UseIfValueExists(dz =>
			{
				composition.DeadZone.Enabled = dz > 0f;
				composition.DeadZone.Size = new Vector2(dz, dz);
			});
			composer.Composition = composition;
		}

		private void AddAim(CameraFollowData data)
		{
			var aim = gameObject.AddComponent<CinemachineRotationComposer>();
			data.Damping.UseIfValueExists(d => aim.Damping = new Vector2(d, d));
		}

		// One value drives whichever projection is active: the Unity camera reads OrthographicSize when
		// orthographic and FieldOfView when perspective, so writing both leaves the dormant field unused.
		private void SetLens(float value)
		{
			var lens = _cam.Lens;
			lens.OrthographicSize = value;
			lens.FieldOfView = value;
			_cam.Lens = lens;
		}

		// Re-resolve targets each frame so tag targets follow entities spawned after build.
		private void Update()
		{
			ApplyTarget(Data.Follow, t => _cam.Follow = t);
			ApplyTarget(Data.LookAt, t => _cam.LookAt = t);
		}

		// Skip unconfigured targets (NullValueProvider) and ones whose entity isn't present this frame
		// (the provider returns null until a tag match spawns / after the target is destroyed).
		private static void ApplyTarget(IValueProvider<Transform> target, Action<Transform> apply)
		{
			if (target is NullValueProvider<Transform>)
			{
				return;
			}

			var transform = target.Get();
			if (transform != null)
			{
				apply(transform);
			}
		}
	}
}
