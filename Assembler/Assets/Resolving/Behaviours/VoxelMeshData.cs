using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class VoxelMeshData : BehaviourData
	{
		public IValueProvider<Mesh> Mesh { get; }
		public IValueProvider<Vector3> Scale { get; }

		public VoxelMeshData(string id,
			IValueProvider<Mesh> mesh,
			IValueProvider<Vector3> scale) : base(id) =>
			(Mesh, Scale) = (mesh, scale);
	}
}
