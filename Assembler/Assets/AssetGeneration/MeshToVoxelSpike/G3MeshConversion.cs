using System.Collections.Generic;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Converts g3 meshes to <see cref="UnityEngine.Mesh"/> for the previewer, carrying optional
    /// per-vertex reprojected colours. Meshes stay in the g3 (right-handed) frame; triangle winding
    /// is reversed so front faces point outward when placed into Unity's left-handed space. Colour
    /// arrays are indexed by g3 vertex id.
    /// </summary>
    public static class G3MeshConversion
    {
        /// <summary>
        /// g3 → Unity, with optional per-vertex colours (indexed by g3 vertex id). Colourless stages
        /// still get a white colour stream so a single vertex-colour material can tint them grey.
        /// </summary>
        public static Mesh ToUnity(g3.DMesh3 mesh, Color32[]? vertexColoursByVid)
        {
            var white = (Color32)Color.white;
            var vertices = new List<Vector3>(mesh.VertexCount);
            var colours = new List<Color32>(mesh.VertexCount);
            var remap = new Dictionary<int, int>(mesh.VertexCount);

            foreach (int vid in mesh.VertexIndices())
            {
                remap[vid] = vertices.Count;
                g3.Vector3d p = mesh.GetVertex(vid);
                vertices.Add(new Vector3((float)p.x, (float)p.y, (float)p.z));
                colours.Add(vertexColoursByVid != null && vid < vertexColoursByVid.Length
                    ? vertexColoursByVid[vid]
                    : white);
            }

            var triangles = new List<int>(mesh.TriangleCount * 3);
            foreach (int tid in mesh.TriangleIndices())
            {
                g3.Index3i tri = mesh.GetTriangle(tid);
                // Reversed winding (a, c, b): compensates the right-handed → left-handed placement so
                // front faces stay outward.
                triangles.Add(remap[tri.a]);
                triangles.Add(remap[tri.c]);
                triangles.Add(remap[tri.b]);
            }

            return Build("SdfRemesh", vertices, colours, triangles);
        }

        /// <summary>
        /// Builds a Unity preview of the <b>original</b> imported mesh, colouring each vertex from the
        /// texture at its own UV (a textured-looking A/B reference), or the flat material colour when
        /// untextured.
        /// </summary>
        public static Mesh OriginalToUnity(ObjToVoxConverter.LoadedModel model)
        {
            g3.DMesh3 mesh = model.Mesh;
            ObjToVoxConverter.ColorSource colors = model.Colors;
            bool textured = colors.HasTexture && model.HasUVs;

            var vertices = new List<Vector3>(mesh.VertexCount);
            var colours = new List<Color32>(mesh.VertexCount);
            var remap = new Dictionary<int, int>(mesh.VertexCount);

            foreach (int vid in mesh.VertexIndices())
            {
                remap[vid] = vertices.Count;
                g3.Vector3d p = mesh.GetVertex(vid);
                vertices.Add(new Vector3((float)p.x, (float)p.y, (float)p.z));

                if (textured)
                {
                    g3.Vector2f uv = mesh.GetVertexUV(vid);
                    colours.Add(colors.Texture!.SampleBilinear(uv.x, uv.y).gamma);
                }
                else
                {
                    colours.Add(colors.FlatColor);
                }
            }

            var triangles = new List<int>(mesh.TriangleCount * 3);
            foreach (int tid in mesh.TriangleIndices())
            {
                g3.Index3i tri = mesh.GetTriangle(tid);
                triangles.Add(remap[tri.a]);
                triangles.Add(remap[tri.c]);
                triangles.Add(remap[tri.b]);
            }

            return Build("Original", vertices, colours, triangles);
        }

        private static Mesh Build(string name, List<Vector3> vertices, List<Color32>? colours, List<int> triangles)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            if (colours != null)
            {
                mesh.SetColors(colours);
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
