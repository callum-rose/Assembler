using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds a Cinemachine <c>FollowZoom</c> extension that auto-adjusts the camera's field of view to hold
	/// the follow target at a constant on-screen width (dolly-zoom style framing).</summary>
	/// <remarks>
	/// This is a <b>modifier</b> behaviour: it needs a virtual camera (<c>camera follow</c>/<c>camera orbit</c>/
	/// <c>camera group</c>) on the same entity and must be listed <b>after</b> it, or initialisation throws. The
	/// zoom needs a follow target to measure distance to, so it pairs with a follow camera. It runs on real frame
	/// time (presentation-only). Applies to perspective cameras (FOV); it has no effect on an orthographic one.
	/// Properties:
	///   Width: Target's desired on-screen width in world units (default Cinemachine's 2). Smaller = more zoomed in.
	///   Damping: How softly the FOV adjusts, in seconds (default 1); 0 is instant.
	///   MinFOV: Lower bound on the field of view in degrees (default 3) — the most it will zoom in.
	///   MaxFOV: Upper bound on the field of view in degrees (default 60) — the most it will zoom out.
	/// </remarks>
	public sealed class CameraZoom : GameBehaviour<CameraZoomData>
	{
		protected override void OnInitialise(CameraZoomData data)
		{
			CameraModifier.RequireVirtualCamera(gameObject, "camera zoom");

			var zoom = gameObject.AddComponent<CinemachineFollowZoom>();
			data.Width.UseIfValueExists(w => zoom.Width = w);
			data.Damping.UseIfValueExists(d => zoom.Damping = d);
			data.MinFOV.UseIfValueExists(min => zoom.FovRange = new Vector2(min, zoom.FovRange.y));
			data.MaxFOV.UseIfValueExists(max => zoom.FovRange = new Vector2(zoom.FovRange.x, max));
		}
	}
}
