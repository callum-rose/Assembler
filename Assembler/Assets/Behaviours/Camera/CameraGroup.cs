using System.Collections.Generic;
using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds a Cinemachine virtual camera that frames a whole group of entities at once: it builds a
	/// <c>TargetGroup</c> from every entity carrying <c>Tag</c> and uses <c>GroupFraming</c> to auto-zoom so they
	/// all stay on screen. The membership is rebuilt each frame, so the group tracks spawns and deaths.</summary>
	/// <remarks>
	/// Blended by the brain on the output <c>camera</c>. Framing/damping run on real frame time (presentation-only)
	/// and never feed back into game logic. The group's bounding centre/size drives both the camera position and
	/// its FOV, so a spread-out group zooms the camera out and a clustered one zooms it in.
	/// Properties:
	///   Tag: Entity tag whose members the camera frames; re-queried every frame so spawned/destroyed entities update the group.
	///   Priority: Virtual-camera priority; the brain shows the highest-priority live vcam.
	///   Damping: How softly the framing reacts as members move, in seconds (default Cinemachine's 2); 0 is instant.
	///   FramingSize: How much of the screen the group should fill, 0..1 (default Cinemachine's 0.8).
	///   Lens: Orthographic size or field of view in degrees, depending on the output camera projection.
	/// </remarks>
	public sealed class CameraGroup : GameBehaviour<CameraGroupData>
	{
		private CinemachineCamera _cam = null!;
		private CinemachineTargetGroup _group = null!;
		private readonly List<Transform> _members = new();

		protected override void OnInitialise(CameraGroupData data)
		{
			_cam = gameObject.AddComponent<CinemachineCamera>();
			data.Priority.UseIfValueExists(p => _cam.Priority = p);
			data.Lens.UseIfValueExists(SetLens);

			_group = gameObject.AddComponent<CinemachineTargetGroup>();
			_cam.Follow = _group.transform;
			_cam.LookAt = _group.transform;

			var framing = gameObject.AddComponent<CinemachineGroupFraming>();
			data.FramingSize.UseIfValueExists(s => framing.FramingSize = s);
			data.Damping.UseIfValueExists(d => framing.Damping = d);
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

		// Rebuild the target group from the current tag matches, but only when the membership actually changes —
		// re-adding identical members every frame would churn Cinemachine's group bookkeeping needlessly.
		private void Update()
		{
			var tag = Data.Tag.Get();
			var current = string.IsNullOrEmpty(tag)
				? (IReadOnlyList<Transform>)System.Array.Empty<Transform>()
				: Data.ResolveByEntityTag(tag);

			if (current.SequenceEqual(_members))
			{
				return;
			}

			_members.Clear();
			_members.AddRange(current);

			_group.Targets.Clear();
			foreach (var member in _members)
			{
				_group.AddMember(member, 1f, 0.5f);
			}
		}
	}
}
