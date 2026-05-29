using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Renders a voxel mesh asset as a child of the entity.</summary>
	/// <remarks>
	/// Properties:
	///   Mesh: Asset reference to the Mesh to display.
	///   Scale: Optional local-space scale multiplier applied to the child renderer.
	/// </remarks>
	public class VoxelMesh : GameBehaviour<VoxelMeshData>
	{
		protected override void OnInitialise(VoxelMeshData data)
		{
			var meshGo = new GameObject("VoxelMesh");
			meshGo.transform.SetParent(transform, false);

			var filter = meshGo.AddComponent<MeshFilter>();
			filter.sharedMesh = data.Mesh.Get();
			meshGo.AddComponent<MeshRenderer>();

			data.Scale.UseIfValueExists(s => meshGo.transform.localScale = s);
		}

		public override void Execute(TriggerContext ctx) { }
	}
}
