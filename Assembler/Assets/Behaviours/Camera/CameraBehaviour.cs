using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds a Unity Camera component to the entity; chooses orthographic vs perspective and sets size.</summary>
	/// <remarks>
	/// Properties:
	///   View: "orthographic" for a 2D-style camera; any other value uses a perspective projection.
	///   Size: Orthographic size in world units (only used when View = "orthographic").
	/// </remarks>
	public class CameraBehaviour : GameBehaviour<CameraData>
	{
		protected override void OnInitialise(CameraData data)
		{
			var camera = gameObject.AddComponent<UnityEngine.Camera>();
			data.Perspective.UseIfValueExists(v => camera.orthographic = v == "orthographic");
			data.Size.UseIfValueExists(v => camera.orthographicSize = v);
		}

		public override void Execute(TriggerContext ctx)
		{
		}
	}
}