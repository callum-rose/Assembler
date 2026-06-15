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
	public class VoxelMesh : GameBehaviour<VoxelMeshData>, INeedsLiveProperties
	{
		public LivePropertyUpdater LiveProperties { get; set; } = null!;

		private GameObject _meshGo;
		private MeshFilter _filter;

		protected override void OnInitialise(VoxelMeshData data)
		{
			// Create the mesh child + filter/renderer once and reuse them across pooled lives: OnInitialise re-runs
			// on every reuse, so a guard keeps the single mesh child rather than spawning a duplicate each life,
			// and re-points the persisted filter. A guard rather than Awake because Awake does not run in edit mode
			// (the sandbox validator / EditMode tests build via OnInitialise).
			if (_meshGo == null)
			{
				_meshGo = new GameObject("VoxelMesh");
				_meshGo.transform.SetParent(transform, false);
				_filter = _meshGo.AddComponent<MeshFilter>();
				_meshGo.AddComponent<MeshRenderer>();
			}

			_filter.sharedMesh = data.Mesh.Get();

			// Live-bind the scale so a !var/!expr/!clock animates the mesh; an omitted Scale falls back to
			// Vector3.one, matching the transform's default (so the no-Scale case is unchanged).
			data.Scale.BindLive(this, s => _meshGo.transform.localScale = s, Vector3.one);
		}
	}
}
