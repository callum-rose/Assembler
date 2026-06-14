using System.Collections.Generic;
using Assembler.Voxels;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assembler.Voxelization
{
	/// <summary>Builds a Unity Mesh for one part grid. Seam so tests and future runtime meshers can swap in.</summary>
	public interface IPartMeshProvider
	{
		Mesh BuildMesh(string partId, VoxelModel grid);
	}

	/// <summary>
	/// Naive culled-face cube mesher with vertex colours: one quad per voxel
	/// face that has no neighbour. Good enough for editor preview and test
	/// scenes; the player app's greedy mesher can replace it behind
	/// <see cref="IPartMeshProvider"/> without format changes.
	/// </summary>
	public sealed class VoxelMeshBuilder : IPartMeshProvider
	{
		private static readonly Vector3Int[] FaceNormals =
		{
			new(1, 0, 0), new(-1, 0, 0),
			new(0, 1, 0), new(0, -1, 0),
			new(0, 0, 1), new(0, 0, -1),
		};

		// Four corners (CCW seen from outside) per face, as cube-corner offsets.
		private static readonly Vector3[][] FaceCorners =
		{
			new Vector3[] { new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1) },
			new Vector3[] { new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0) },
			new Vector3[] { new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0) },
			new Vector3[] { new(0, 0, 1), new(0, 0, 0), new(1, 0, 0), new(1, 0, 1) },
			new Vector3[] { new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1) },
			new Vector3[] { new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0) },
		};

		public Mesh BuildMesh(string partId, VoxelModel grid)
		{
			var vertices = new List<Vector3>();
			var colours = new List<Color32>();
			var normals = new List<Vector3>();
			var triangles = new List<int>();

			foreach (var kv in grid.Voxels)
			{
				var cell = kv.Key;
				var colour = grid.Palette[kv.Value - 1];
				for (var face = 0; face < 6; face++)
				{
					if (grid.Voxels.ContainsKey(cell + FaceNormals[face]))
					{
						continue;
					}

					var baseIndex = vertices.Count;
					foreach (var corner in FaceCorners[face])
					{
						vertices.Add((Vector3)cell + corner);
						colours.Add(colour);
						normals.Add(FaceNormals[face]);
					}

					triangles.Add(baseIndex);
					triangles.Add(baseIndex + 1);
					triangles.Add(baseIndex + 2);
					triangles.Add(baseIndex);
					triangles.Add(baseIndex + 2);
					triangles.Add(baseIndex + 3);
				}
			}

			var mesh = new Mesh
			{
				name = partId,
				indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
			};
			mesh.SetVertices(vertices);
			mesh.SetColors(colours);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
