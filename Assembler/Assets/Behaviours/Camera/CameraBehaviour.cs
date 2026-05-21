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
		private UnityEngine.Camera _camera;

		private void Awake()
		{
			_camera = gameObject.AddComponent<UnityEngine.Camera>();
		}

		protected override void OnInitialise(CameraData data)
		{
			data.Perspective.UseIfValueExists(v => _camera.orthographic = v == "orthographic");
			data.Size.UseIfValueExists(v => _camera.orthographicSize = v);
		}

		public override void Execute()
		{
		}
	}
}