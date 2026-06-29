using UnityEngine;

namespace Assembler.VoxelPipeline
{
    /// <summary>
    /// Pipeline step 3 (opt-in) — forces bilateral symmetry by mirroring one half of the model
    /// about a plane onto the other. <b>Off by default</b>: forcing symmetry erases intentional
    /// asymmetry (an eyepatch, a raised paw, a logo on one door).
    ///
    /// The plane is found by <b>known axis + offset-solve + confidence gate</b> (§6.4): the axis
    /// is fixed (default left/right = <see cref="SymmetryAxis.X"/>); the <i>offset</i> along it is
    /// searched for the best mirror overlap (occupancy agreement, weighted by Oklab colour
    /// agreement); and the best overlap is gated on a confidence score. Below the threshold the
    /// model is treated as <i>not</i> symmetric and left untouched, unless <see cref="Options.Force"/>
    /// overrides the gate for a stubborn asset.
    ///
    /// Runs while colour is still raw (before de-light), so the mirrored half carries the same
    /// pre-flattening colours as its source and the later colour steps treat both halves alike.
    /// </summary>
    public static class Mirror
    {
        public readonly struct Options
        {
            /// <summary>The axis the mirror plane is perpendicular to (default left/right).</summary>
            public SymmetryAxis Axis { get; }

            /// <summary>Minimum overlap score (0..1) to auto-apply. Below this the model is left as-is.</summary>
            public float ConfidenceThreshold { get; }

            /// <summary>Apply at the best-scoring plane even when the confidence gate fails.</summary>
            public bool Force { get; }

            /// <summary>Share of the overlap score (0..1) that comes from colour agreement vs raw occupancy.</summary>
            public float ColourWeight { get; }

            /// <summary>Oklab distance at which two mirrored voxels count as fully disagreeing on colour.</summary>
            public float ColourTolerance { get; }

            public Options(
                SymmetryAxis axis,
                float confidenceThreshold,
                bool force,
                float colourWeight,
                float colourTolerance)
            {
                Axis = axis;
                ConfidenceThreshold = Mathf.Clamp01(confidenceThreshold);
                Force = force;
                ColourWeight = Mathf.Clamp01(colourWeight);
                ColourTolerance = Mathf.Max(0f, colourTolerance);
            }

            public static Options Default => new Options(SymmetryAxis.X, 0.85f, false, 0.25f, 0.15f);
        }

        /// <summary>Outcome of a mirror attempt — exposed so the gate behaviour is testable.</summary>
        public readonly struct Result
        {
            /// <summary>Whether the model was actually modified (gate passed or forced).</summary>
            public bool Applied { get; }

            /// <summary>Best overlap score found (0..1).</summary>
            public float Score { get; }

            /// <summary>The winning plane, encoded as the constant sum <c>along + mirror(along)</c>.</summary>
            public int PlaneSum { get; }

            public Result(bool applied, float score, int planeSum)
            {
                Applied = applied;
                Score = score;
                PlaneSum = planeSum;
            }
        }

        public static Result Apply(VoxModel model, Options options)
        {
            int axisSize = Size(model, options.Axis);
            if (axisSize == 0)
            {
                return new Result(false, 0f, 0);
            }

            // Occupied indices + their Oklab colours, gathered once for the offset search.
            var lab = new OklabColor[model.Occupied.Length];
            int lo = int.MaxValue, hi = int.MinValue, occupied = 0;
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                lab[i] = OklabColor.FromColor32(model.Colors[i]);
                int along = Component(model, options.Axis, i);
                lo = Mathf.Min(lo, along);
                hi = Mathf.Max(hi, along);
                occupied++;
            }
            if (occupied == 0)
            {
                return new Result(false, 0f, 0);
            }

            // Offset-solve: the mirror plane lives at sum/2, so a voxel at `along` maps to
            // `sum - along`. Search every plane that could map the model's span onto itself.
            float colourTolerance = options.ColourTolerance;
            float colourWeight = options.ColourWeight;
            int bestSum = 2 * lo;
            float bestScore = -1f;
            int centreSum = axisSize - 1;
            for (int sum = 2 * lo; sum <= 2 * hi; sum++)
            {
                float score = ScoreFor(model, options.Axis, lab, sum, occupied, colourWeight, colourTolerance);
                // Prefer the higher score; on a tie prefer the plane nearest the grid centre.
                if (score > bestScore ||
                    (Mathf.Approximately(score, bestScore) &&
                     Mathf.Abs(sum - centreSum) < Mathf.Abs(bestSum - centreSum)))
                {
                    bestScore = score;
                    bestSum = sum;
                }
            }

            bool apply = options.Force || bestScore >= options.ConfidenceThreshold;
            if (apply)
            {
                ForceSymmetric(model, options.Axis, bestSum);
            }
            return new Result(apply, Mathf.Max(0f, bestScore), bestSum);
        }

        private static float ScoreFor(
            VoxModel model,
            SymmetryAxis axis,
            OklabColor[] lab,
            int sum,
            int occupied,
            float colourWeight,
            float colourTolerance)
        {
            int axisSize = Size(model, axis);
            float matched = 0f;
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                int along = Component(model, axis, i);
                int mirrorAlong = sum - along;
                if (mirrorAlong < 0 || mirrorAlong >= axisSize)
                {
                    continue; // reflects out of bounds — counts against symmetry.
                }
                int j = WithComponent(model, axis, i, mirrorAlong);
                if (!model.Occupied[j])
                {
                    continue;
                }
                float colourAgreement = colourTolerance <= 0f
                    ? 1f
                    : Mathf.Clamp01(1f - lab[i].DistanceTo(lab[j]) / colourTolerance);
                matched += (1f - colourWeight) + colourWeight * colourAgreement;
            }
            return matched / occupied;
        }

        /// <summary>
        /// Overwrites the half with fewer occupied voxels with the mirror of the other, keeping the
        /// detail-richer side as the source. Voxels lying exactly on the plane are left untouched.
        /// Reads only source-side cells while writing only destination-side cells, so it is safe
        /// in place.
        /// </summary>
        private static void ForceSymmetric(VoxModel model, SymmetryAxis axis, int sum)
        {
            int axisSize = Size(model, axis);

            int lowCount = 0, highCount = 0;
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                int side = 2 * Component(model, axis, i) - sum;
                if (side < 0)
                {
                    lowCount++;
                }
                else if (side > 0)
                {
                    highCount++;
                }
            }
            // The detail-richer half is the source. On an exact tie the low half wins arbitrarily
            // (equal counts don't imply equal contents, but neither side is preferable).
            bool sourceIsLow = lowCount >= highCount;

            for (int i = 0; i < model.Occupied.Length; i++)
            {
                int along = Component(model, axis, i);
                int side = 2 * along - sum;
                bool isDestination = sourceIsLow ? side > 0 : side < 0;
                if (!isDestination)
                {
                    continue; // leave the source half and the on-plane column alone.
                }

                int mirrorAlong = sum - along;
                if (mirrorAlong < 0 || mirrorAlong >= axisSize)
                {
                    model.Occupied[i] = false;
                    continue;
                }
                int j = WithComponent(model, axis, i, mirrorAlong);
                model.Occupied[i] = model.Occupied[j];
                model.Colors[i] = model.Colors[j];
            }
        }

        private static int Size(VoxModel model, SymmetryAxis axis) => axis switch
        {
            SymmetryAxis.X => model.X,
            SymmetryAxis.Y => model.Y,
            _ => model.Z,
        };

        private static int Component(VoxModel model, SymmetryAxis axis, int index)
        {
            (int x, int y, int z) = model.Coords(index);
            return axis switch
            {
                SymmetryAxis.X => x,
                SymmetryAxis.Y => y,
                _ => z,
            };
        }

        /// <summary>The index of the voxel sharing <paramref name="index"/>'s coords but with the axis component replaced.</summary>
        private static int WithComponent(VoxModel model, SymmetryAxis axis, int index, int along)
        {
            (int x, int y, int z) = model.Coords(index);
            return axis switch
            {
                SymmetryAxis.X => model.Index(along, y, z),
                SymmetryAxis.Y => model.Index(x, along, z),
                _ => model.Index(x, y, along),
            };
        }
    }
}
