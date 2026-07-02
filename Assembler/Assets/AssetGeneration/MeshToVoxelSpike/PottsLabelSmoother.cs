using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Edge-aware Potts label smoothing over the palette assignment — kills AO-speckle faux
    /// gradients without blurring real colour-region boundaries. ICM over the 6-adjacency, labels
    /// only: the unary term anchors each voxel to its SAMPLED (pre-palette) colour, the pairwise
    /// term charges differing neighbour labels β·exp(−(d_uv/σ)²) so the penalty melts away exactly
    /// where the source colours already disagree (a real edge stays pinned). β is normalised from
    /// the model's own unary margins so one strength knob behaves the same across models.
    /// </summary>
    public static class PottsLabelSmoother
    {
        /// <summary>Source-colour difference (Oklab) at which the pairwise penalty has decayed to 1/e.</summary>
        private const float EdgeSigma = 0.08f;

        private const int MaxSweeps = 10;

        private static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
        };

        /// <summary>
        /// Smooth <paramref name="labels"/> (−1 = unlabelled, indexed by <see cref="VoxelGrid.Index"/>)
        /// against the voxels' sampled colours. Returns new labels; strength 0 short-circuits to a copy.
        /// </summary>
        public static int[] Smooth(
            VoxelGrid grid, Color32[] sampledColours, int[] labels, Color32[] palette, float strength)
        {
            var result = (int[])labels.Clone();
            if (strength <= 0f || palette.Length <= 1)
            {
                return result;
            }

            var paletteLab = new OklabColor[palette.Length];
            for (int p = 0; p < palette.Length; p++)
            {
                paletteLab[p] = OklabColor.FromColor32(palette[p]);
            }

            // Sampled colours in Oklab, for both the unary anchors and the pairwise edge attenuation.
            var sampleLab = new OklabColor[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                if (result[i] >= 0)
                {
                    sampleLab[i] = OklabColor.FromColor32(sampledColours[i]);
                }
            }

            float beta = strength * MeanUnaryMargin(result, sampleLab, paletteLab);
            if (beta <= 0f)
            {
                return result;
            }

            for (int sweep = 0; sweep < MaxSweeps; sweep++)
            {
                if (!SweepOnce(grid, result, sampleLab, paletteLab, beta))
                {
                    break;
                }
            }
            return result;
        }

        // β normalisation: the mean gap between each voxel's best and second-best unary. A strength
        // of 1 therefore prices one label flip at "the average voxel's ambiguity", independent of
        // how spread the model's colours are.
        private static float MeanUnaryMargin(int[] labels, OklabColor[] sampleLab, OklabColor[] paletteLab)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] < 0)
                {
                    continue;
                }

                float best = float.MaxValue, second = float.MaxValue;
                for (int p = 0; p < paletteLab.Length; p++)
                {
                    float u = sampleLab[i].SquaredDistanceTo(paletteLab[p]);
                    if (u < best)
                    {
                        second = best;
                        best = u;
                    }
                    else if (u < second)
                    {
                        second = u;
                    }
                }
                sum += second - best;
                count++;
            }
            return count > 0 ? (float)(sum / count) : 0f;
        }

        private static bool SweepOnce(
            VoxelGrid grid, int[] labels, OklabColor[] sampleLab, OklabColor[] paletteLab, float beta)
        {
            bool changed = false;
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (labels[i] < 0)
                        {
                            continue;
                        }

                        int best = labels[i];
                        float bestEnergy = float.MaxValue;
                        for (int p = 0; p < paletteLab.Length; p++)
                        {
                            float energy = sampleLab[i].SquaredDistanceTo(paletteLab[p])
                                + PairwiseEnergy(grid, labels, sampleLab, beta, x, y, z, i, p);
                            if (energy < bestEnergy)
                            {
                                bestEnergy = energy;
                                best = p;
                            }
                        }

                        if (best != labels[i])
                        {
                            labels[i] = best;
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        // Σ over labelled 6-neighbours with a different label: β·exp(−(d_uv/σ)²), where d_uv is the
        // SOURCE-colour distance — a strong source edge attenuates the penalty to ~0, pinning it.
        private static float PairwiseEnergy(
            VoxelGrid grid, int[] labels, OklabColor[] sampleLab, float beta,
            int x, int y, int z, int i, int candidateLabel)
        {
            float energy = 0f;
            foreach ((int dx, int dy, int dz) in FaceNeighbours)
            {
                int px = x + dx, py = y + dy, pz = z + dz;
                if (!grid.InBounds(px, py, pz))
                {
                    continue;
                }
                int n = grid.Index(px, py, pz);
                if (labels[n] < 0 || labels[n] == candidateLabel)
                {
                    continue;
                }

                float d = sampleLab[i].DistanceTo(sampleLab[n]) / EdgeSigma;
                energy += beta * Mathf.Exp(-d * d);
            }
            return energy;
        }
    }
}
