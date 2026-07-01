using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// The core new colour idea: instead of binning supersampled surface texels into solid voxels,
    /// transfer colour by <b>closest point on the original surface → barycentric UV → texture
    /// sample</b>. For a smooth-remesh vertex or a blocky-voxel centre we find the nearest triangle
    /// on the <i>original</i> Meshy mesh, interpolate its wedge UVs at the closest point, and read
    /// the texture there (or the flat material colour when untextured). This keeps the reprojected
    /// colour tied to the real surface the point represents rather than a voxel-averaged blob.
    /// </summary>
    public static class ColourReprojector
    {
        /// <summary>
        /// Per-vertex reprojected colour for a remeshed <paramref name="mesh"/>, indexed by g3 vertex
        /// id (array length <c>MaxVertexID</c>; only valid ids are written).
        /// </summary>
        public static Color32[] SampleVertices(
            g3.DMesh3 mesh,
            ObjToVoxConverter.LoadedModel model,
            g3.DMeshAABBTree3 tree,
            bool normalConsistency,
            g3.CachingDenseGridTrilinearImplicit? field)
        {
            var colours = new Color32[mesh.MaxVertexID];
            foreach (int vid in mesh.VertexIndices())
            {
                g3.Vector3d p = mesh.GetVertex(vid);
                colours[vid] = Sample(p, model, tree, normalConsistency, field);
            }
            return colours;
        }

        /// <summary>
        /// Per-voxel reprojected colour for an occupancy grid, indexed by <see cref="VoxelGrid.Index"/>.
        /// Each occupied voxel is coloured from the nearest original-surface point to its centre.
        /// </summary>
        public static Color32[] SampleVoxels(
            VoxelGrid occupancy,
            ObjToVoxConverter.LoadedModel model,
            g3.DMeshAABBTree3 tree,
            bool normalConsistency,
            g3.CachingDenseGridTrilinearImplicit? field)
        {
            var colours = new Color32[occupancy.Occupied.Length];
            for (int z = 0; z < occupancy.NZ; z++)
            {
                for (int y = 0; y < occupancy.NY; y++)
                {
                    for (int x = 0; x < occupancy.NX; x++)
                    {
                        int i = occupancy.Index(x, y, z);
                        if (!occupancy.Occupied[i])
                        {
                            continue;
                        }
                        colours[i] = Sample(occupancy.Center(x, y, z), model, tree, normalConsistency, field);
                    }
                }
            }
            return colours;
        }

        private static Color32 Sample(
            g3.Vector3d p,
            ObjToVoxConverter.LoadedModel model,
            g3.DMeshAABBTree3 tree,
            bool normalConsistency,
            g3.CachingDenseGridTrilinearImplicit? field)
        {
            ObjToVoxConverter.ColorSource colors = model.Colors;
            if (!colors.HasTexture || !model.HasUVs)
            {
                return colors.FlatColor;
            }

            int tid = tree.FindNearestTriangle(p);
            if (tid < 0)
            {
                return colors.FlatColor;
            }

            g3.DMesh3 orig = model.Mesh;
            g3.Vector3d v0 = g3.Vector3d.Zero, v1 = g3.Vector3d.Zero, v2 = g3.Vector3d.Zero;
            orig.GetTriVertices(tid, ref v0, ref v1, ref v2);

            // Optional wrong-side reject: on a thin wall the nearest triangle can be the back face,
            // whose texels carry the interior/AO colour. Compare the triangle normal against the
            // outward SDF gradient at the sample point; if they oppose, fall back to the flat colour
            // rather than staining the voxel with a back-face texel. Off by default.
            if (normalConsistency && field != null && IsWrongSide(v0, v1, v2, p, field))
            {
                return colors.FlatColor;
            }

            var dist = new g3.DistPoint3Triangle3(p, new g3.Triangle3d(v0, v1, v2));
            dist.GetSquared();
            g3.Vector3d bary = dist.TriangleBaryCoords;

            g3.Index3i tri = orig.GetTriangle(tid);
            g3.Vector2f uv0 = orig.GetVertexUV(tri.a);
            g3.Vector2f uv1 = orig.GetVertexUV(tri.b);
            g3.Vector2f uv2 = orig.GetVertexUV(tri.c);

            float u = (float)(bary.x * uv0.x + bary.y * uv1.x + bary.z * uv2.x);
            float v = (float)(bary.x * uv0.y + bary.y * uv1.y + bary.z * uv2.y);

            // The snapshot samples in linear space (mirroring the GPU sampler); re-encode to gamma so
            // the stored vertex colour isn't darkened, matching the existing converter.
            return colors.Texture!.SampleBilinear(u, v).gamma;
        }

        private static bool IsWrongSide(
            g3.Vector3d v0, g3.Vector3d v1, g3.Vector3d v2, g3.Vector3d p,
            g3.CachingDenseGridTrilinearImplicit field)
        {
            // Triangle normal (e1 × e2) and the outward SDF gradient, via explicit components to
            // avoid depending on Vector3d.Cross/Dot helpers.
            double e1x = v1.x - v0.x, e1y = v1.y - v0.y, e1z = v1.z - v0.z;
            double e2x = v2.x - v0.x, e2y = v2.y - v0.y, e2z = v2.z - v0.z;
            double nx = e1y * e2z - e1z * e2y;
            double ny = e1z * e2x - e1x * e2z;
            double nz = e1x * e2y - e1y * e2x;

            g3.Vector3d outward = field.Gradient(ref p);
            return nx * outward.x + ny * outward.y + nz * outward.z < 0;
        }
    }
}
