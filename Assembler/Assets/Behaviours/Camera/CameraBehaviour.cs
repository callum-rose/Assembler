using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds the output Unity Camera plus a Cinemachine brain, so virtual cameras (e.g. <c>camera follow</c>)
	/// can drive and blend this camera. Also adds an impulse listener so <c>camera shake</c> is visible.</summary>
	/// <remarks>
	/// Cinemachine blends and damping run on the brain using real frame time, outside the deterministic game clock.
	/// This is acceptable because cameras are presentation-only and must never feed values back into game logic.
	/// Properties:
	///   View: "orthographic" for a 2D-style camera, or "perspective" (default) for a 3D projection.
	///   Size: Orthographic size in world units (only used when View = "orthographic").
	///   DefaultBlend: Default blend time in seconds when the brain cuts between virtual cameras (0 = instant cut).
	/// </remarks>
	public class CameraBehaviour : GameBehaviour<CameraData>
	{
		protected override void OnInitialise(CameraData data)
		{
			var camera = gameObject.AddComponent<UnityEngine.Camera>();
			camera.orthographic = data.View.ValueOr(CameraProjection.Perspective) == CameraProjection.Orthographic;
			data.Size.UseIfValueExists(v => camera.orthographicSize = v);

			var brain = gameObject.AddComponent<CinemachineBrain>();
			data.DefaultBlend.UseIfValueExists(seconds =>
				brain.DefaultBlend = new CinemachineBlendDefinition(
					seconds > 0f ? CinemachineBlendDefinition.Styles.EaseInOut : CinemachineBlendDefinition.Styles.Cut,
					seconds));

			// Lets one-shot Cinemachine Impulse signals (camera shake) move this camera.
			gameObject.AddComponent<CinemachineImpulseListener>();
		}
	}
}
