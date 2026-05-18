using Assembler.Resolving;

namespace Assembler.Behaviours.Camera
{
	public class CameraBehaviour : GameBehaviour<CameraData>
	{
		protected override void OnInitialise(CameraData data)
		{
			var camera = gameObject.AddComponent<UnityEngine.Camera>();
			data.Perspective.UseIfValueExists(v => camera.orthographic = v == "orthographic");
			data.Size.UseIfValueExists(v => camera.orthographicSize = v);
		}

		public override void Execute()
		{
		}
	}
}