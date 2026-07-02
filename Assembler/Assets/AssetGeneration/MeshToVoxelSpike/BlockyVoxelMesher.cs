using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// The primary output: a face-culled cube mesh straight from the SDF occupancy grid — one quad
    /// per exposed voxel face, hard edges, no smoothing, each voxel a single flat reprojected colour.
    /// That is the Crossy-Road blocky read. The face-culling layout follows
    /// <c>Assets/Voxelization/Runtime/VoxelMeshBuilder.cs</c>. Cubes are placed in the mesh's own
    /// (g3) world frame — node <c>(x,y,z)</c> centred at <c>Origin + (x,y,z)·CellSize</c> — so the
    /// blocky model overlays the smooth remesh built from the same grid.
    /// </summary>
    public static class BlockyVoxelMesher
    {
        private static readonly Vector3Int[] FaceNormals =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };

        // Four corners (CCW seen from outside) per face, as unit-cube-corner offsets.
        private static readonly Vector3[][] FaceCorners =
        {
            new Vector3[] { new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1) },
            new Vector3[] { new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0) },
            new Vector3[] { new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0) },
            new Vector3[] { new(0, 0, 1), new(0, 0, 0), new(1, 0, 0), new(1, 0, 1) },
            new Vector3[] { new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1) },
            new Vector3[] { new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0) },
        };

        /// <summary>Build the flat-shaded blocky cube mesh; <paramref name="voxelColours"/> is indexed by <see cref="VoxelGrid.Index"/>.</summary>
        public static Mesh Build(VoxelGrid grid, Color32[] voxelColours)
        {
            var vertices = new List<Vector3>();
            var colours = new List<Color32>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            var origin = new Vector3((float)grid.Origin.x, (float)grid.Origin.y, (float)grid.Origin.z);
            float cell = (float)grid.CellSize;

            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (!grid.Occupied[i])
                        {
                            continue;
                        }

                        Color32 colour = voxelColours[i];
                        for (int face = 0; face < 6; face++)
                        {
                            Vector3Int n = FaceNormals[face];
                            if (grid.IsOccupied(x + n.x, y + n.y, z + n.z))
                            {
                                continue;
                            }

                            int baseIndex = vertices.Count;
                            foreach (Vector3 corner in FaceCorners[face])
                            {
                                // Cube centred on the grid node: corners span node ± ½ cell.
                                Vector3 local = new Vector3(x, y, z) + corner - new Vector3(0.5f, 0.5f, 0.5f);
                                vertices.Add(origin + local * cell);
                                colours.Add(colour);
                                normals.Add(n);
                            }

                            triangles.Add(baseIndex);
                            triangles.Add(baseIndex + 1);
                            triangles.Add(baseIndex + 2);
                            triangles.Add(baseIndex);
                            triangles.Add(baseIndex + 2);
                            triangles.Add(baseIndex + 3);
                        }
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "BlockyVoxel",
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
