using System.Collections.Generic;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Robust per-voxel colour sampling: instead of one centre sample per voxel (which a single
    /// purple UV-gutter texel or AO-shadowed hit can poison), each surface voxel takes the centre
    /// plus four deterministic jittered samples per exposed face, all projected through the shared
    /// nearest-triangle sampler, then aggregates with an Oklab medoid — the member colour closest
    /// to all the others, so a 1-in-9 outlier simply loses the vote instead of tinting an average.
    /// Interior voxels keep the single centre sample.
    /// </summary>
    public static class MultiSampleColour
    {
        private static readonly Vector3Int[] FaceNormals =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };

        // Deterministic in-face jitter (fractions of the cell size along the two face tangents).
        // Deliberately not at ±½ so samples stay inside this voxel's face, away from its edges.
        private static readonly Vector2[] FaceJitter =
        {
            new(-0.3f, -0.3f), new(0.3f, -0.3f), new(-0.3f, 0.3f), new(0.3f, 0.3f),
        };

        /// <summary>Per-voxel medoid-aggregated colours, indexed by <see cref="VoxelGrid.Index"/>.</summary>
        public static Color32[] Sample(
            VoxelGrid grid,
            ObjToVoxConverter.LoadedModel model,
            g3.DMeshAABBTree3 tree,
            bool normalConsistency,
            g3.DenseGridTrilinearImplicit? field)
        {
            var colours = new Color32[grid.Occupied.Length];
            var samples = new List<Color32>(25);

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

                        g3.Vector3d centre = grid.Center(x, y, z);
                        samples.Clear();
                        samples.Add(ColourReprojector.SamplePoint(centre, model, tree, normalConsistency, field));

                        double half = grid.CellSize * 0.5;
                        foreach (Vector3Int n in FaceNormals)
                        {
                            if (grid.IsOccupied(x + n.x, y + n.y, z + n.z))
                            {
                                continue;
                            }

                            var normal = new g3.Vector3d(n.x, n.y, n.z);
                            (g3.Vector3d tangentU, g3.Vector3d tangentV) = FaceTangents(n);
                            g3.Vector3d faceCentre = centre + normal * half;

                            foreach (Vector2 jitter in FaceJitter)
                            {
                                g3.Vector3d p = faceCentre
                                    + tangentU * (jitter.x * grid.CellSize)
                                    + tangentV * (jitter.y * grid.CellSize);
                                samples.Add(ColourReprojector.SamplePoint(p, model, tree, normalConsistency, field));
                            }
                        }

                        colours[i] = samples.Count == 1 ? samples[0] : OklabMedoid(samples);
                    }
                }
            }
            return colours;
        }

        /// <summary>
        /// The member colour with the smallest summed Oklab distance to all others (ties → lowest
        /// index). Robust to a minority of outliers, unlike a mean.
        /// </summary>
        public static Color32 OklabMedoid(IReadOnlyList<Color32> colours)
        {
            if (colours.Count == 1)
            {
                return colours[0];
            }

            var labs = new OklabColor[colours.Count];
            for (int i = 0; i < colours.Count; i++)
            {
                labs[i] = OklabColor.FromColor32(colours[i]);
            }

            int best = 0;
            float bestSum = float.MaxValue;
            for (int i = 0; i < labs.Length; i++)
            {
                float sum = 0f;
                for (int j = 0; j < labs.Length; j++)
                {
                    sum += labs[i].DistanceTo(labs[j]);
                }
                if (sum < bestSum)
                {
                    bestSum = sum;
                    best = i;
                }
            }
            return colours[best];
        }

        // The two axes perpendicular to a face normal, picked deterministically.
        private static (g3.Vector3d u, g3.Vector3d v) FaceTangents(Vector3Int normal) =>
            normal.x != 0
                ? (new g3.Vector3d(0, 1, 0), new g3.Vector3d(0, 0, 1))
                : normal.y != 0
                    ? (new g3.Vector3d(1, 0, 0), new g3.Vector3d(0, 0, 1))
                    : (new g3.Vector3d(1, 0, 0), new g3.Vector3d(0, 1, 0));
    }
}
