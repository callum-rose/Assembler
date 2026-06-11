using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds a Cinemachine virtual camera that orbits a target entity at a fixed radius and height
	/// (third-person / orbital framing), blended by the brain on the output <c>camera</c>. Mirrors
	/// <c>camera follow</c> but uses an orbital body rather than a screen/offset rig.</summary>
	/// <remarks>
	/// The follow target is re-resolved every frame, so a tag target tracks entities spawned after build. Damping
	/// runs on real frame time (presentation-only) and never feeds back into game logic. The camera holds its
	/// starting orbit angle; driving the orbit from input is intentionally out of scope here.
	/// Properties:
	///   Target [Tag/Id]: Entity to orbit, as { Tag: &lt;entity-tag&gt; } or { Id: &lt;entity-id&gt; }.
	///   Radius: Orbit distance from the target in world units (default Cinemachine's 10).
	///   Height: Vertical offset above the target the camera orbits around (default 0).
	///   Damping: How softly the camera tracks the target (seconds-ish); 0 is instant.
	///   Priority: Virtual-camera priority; the brain shows the highest-priority live vcam.
	///   Lens: Orthographic size or field of view in degrees, depending on the output camera projection.
	/// </remarks>
	public sealed class CameraOrbit : GameBehaviour<CameraOrbitData>
	{
		private CinemachineCamera _cam = null!;
		private CinemachineOrbitalFollow _orbit = null!;

		protected override void OnInitialise(CameraOrbitData data)
		{
			_cam = gameObject.AddComponent<CinemachineCamera>();
			data.Priority.UseIfValueExists(p => _cam.Priority = p);
			data.Lens.UseIfValueExists(SetLens);

			_orbit = gameObject.AddComponent<CinemachineOrbitalFollow>();
			data.Radius.UseIfValueExists(r => _orbit.Radius = r);
			data.Height.UseIfValueExists(h => _orbit.TargetOffset = new Vector3(0f, h, 0f));
			data.Damping.UseIfValueExists(d =>
			{
				var tracker = _orbit.TrackerSettings;
				tracker.PositionDamping = new Vector3(d, d, d);
				_orbit.TrackerSettings = tracker;
			});
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

		// Re-resolve the target each frame so a tag target follows entities spawned after build.
		private void Update()
		{
			if (Data.Follow is NullValueProvider<Transform>)
			{
				return;
			}

			var target = Data.Follow.Get();
			if (target != null)
			{
				_cam.Follow = target;
			}
		}
	}
}
