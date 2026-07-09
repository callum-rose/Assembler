using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Builds a culled-face cube <see cref="Mesh"/> for a <see cref="VoxelModel"/> — one quad per
    /// voxel face with no neighbour — with a fixed directional shade baked into the vertex colours.
    /// Baking the shade means an <i>unlit</i> vertex-colour shader still conveys 3D form, so the
    /// render doesn't depend on a light rig or a lit URP shader (both of which are fussier to drive
    /// reliably from an editor tool). The mesh lives in the model's own integer grid space (voxel
    /// cell (x,y,z) fills world [x,x+1]³), so it renders exactly where <see cref="VoxelRaycaster"/>
    /// expects the voxels to be, keeping render and reprojection aligned.
    /// </summary>
    public static class VoxelPreviewMesh
    {
        private const float Ambient = 0.45f;
        private const float Diffuse = 0.55f;
        private static readonly Vector3 LightFrom = new Vector3(0.4f, 0.3f, 1f).normalized;

        private static readonly Vector3Int[] FaceNormals =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };

        // Four corners per face (winding is irrelevant — the render material is two-sided).
        private static readonly Vector3[][] FaceCorners =
        {
            new Vector3[] { new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1) },
            new Vector3[] { new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0) },
            new Vector3[] { new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0) },
            new Vector3[] { new(0, 0, 1), new(0, 0, 0), new(1, 0, 0), new(1, 0, 1) },
            new Vector3[] { new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1) },
            new Vector3[] { new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0) },
        };

        public static Mesh Build(VoxelModel model)
        {
            var vertices = new List<Vector3>();
            var colours = new List<Color32>();
            var triangles = new List<int>();

            foreach (var kv in model.Voxels)
            {
                Vector3Int cell = kv.Key;
                Color32 baseColour = ColourFor(model, kv.Value);

                for (int face = 0; face < 6; face++)
                {
                    if (model.Voxels.ContainsKey(cell + FaceNormals[face]))
                    {
                        continue; // interior face — culled
                    }

                    float shade = Ambient + Diffuse * Mathf.Max(0f, Vector3.Dot((Vector3)FaceNormals[face], LightFrom));
                    Color32 lit = Shade(baseColour, shade);

                    int baseIndex = vertices.Count;
                    foreach (Vector3 corner in FaceCorners[face])
                    {
                        vertices.Add((Vector3)cell + corner);
                        colours.Add(lit);
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
                name = "EyePlacementPreview",
                indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color32 ColourFor(VoxelModel model, byte index)
        {
            int slot = index - 1; // voxel values are 1-based into the compact palette
            return slot >= 0 && slot < model.Palette.Length
                ? model.Palette[slot]
                : new Color32(180, 180, 180, 255);
        }

        private static Color32 Shade(Color32 c, float shade) => new(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * shade), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * shade), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * shade), 0, 255),
            255);
    }
}
