using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// Pipeline step 3 (opt-in) — turns a near-cylindrical asset into a true solid of revolution
    /// about an axis. <b>Off by default</b>, and scoped to <b>standalone wheel/cylinder assets</b>
    /// only (no segmentation, so no extracting a wheel from a whole vehicle).
    ///
    /// Why this and not just <see cref="Mirror"/>: mirroring enforces 2-/4-fold symmetry, but a
    /// mirrored lumpy circle is still a lumpy octagon. Roundness needs revolve. We take the radial
    /// profile — occupancy as a function of (axial position × radius from the axis) — and make every
    /// ring at a given radius identical: a ring is filled iff a majority of its cells were occupied,
    /// then the whole ring is set that way. The result is round and rotationally symmetric.
    ///
    /// The axis is specified (default <see cref="SymmetryAxis.Y"/>); the centre in the plane
    /// perpendicular to it is the occupied centroid. Runs while colour is still raw; each ring takes
    /// the average of its occupied colours, which the later de-light/palette steps then flatten.
    /// </summary>
    public static class Revolve
    {
        public readonly struct Options
        {
            /// <summary>The spin axis the profile is revolved about (default up = <see cref="SymmetryAxis.Y"/>).</summary>
            public SymmetryAxis Axis { get; }

            /// <summary>A ring is filled when at least this fraction (0..1) of its cells were occupied.</summary>
            public float FillThreshold { get; }

            public Options(SymmetryAxis axis, float fillThreshold)
            {
                Axis = axis;
                FillThreshold = Mathf.Clamp01(fillThreshold);
            }

            public static Options Default => new Options(SymmetryAxis.Y, 0.5f);
        }

        public static void Apply(VoxModel model, Options options)
        {
            (int sizeA, int sizeP, int sizeQ) = Dimensions(model, options.Axis);
            if (sizeA == 0 || sizeP == 0 || sizeQ == 0)
            {
                return;
            }

            // Centre in the plane perpendicular to the axis = occupied centroid.
            double sumP = 0, sumQ = 0;
            int occupied = 0;
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                (int _, int p, int q) = Decompose(model, options.Axis, i);
                sumP += p;
                sumQ += q;
                occupied++;
            }
            if (occupied == 0)
            {
                return;
            }
            float centreP = (float)(sumP / occupied);
            float centreQ = (float)(sumQ / occupied);

            // Radial profile, binned by (axial slice, integer radius). `total` counts every grid
            // cell in a ring; `filled` counts the occupied ones; the colour sums build the ring's
            // representative colour.
            int maxBin = Mathf.CeilToInt(Mathf.Sqrt(sizeP * (float)sizeP + sizeQ * (float)sizeQ)) + 1;
            var total = new int[sizeA * maxBin];
            var filled = new int[sizeA * maxBin];
            var sumR = new long[sizeA * maxBin];
            var sumG = new long[sizeA * maxBin];
            var sumB = new long[sizeA * maxBin];

            for (int i = 0; i < model.Occupied.Length; i++)
            {
                (int a, int p, int q) = Decompose(model, options.Axis, i);
                int bin = a * maxBin + RadiusBin(p - centreP, q - centreQ, maxBin);
                total[bin]++;
                if (model.Occupied[i])
                {
                    filled[bin]++;
                    Color32 c = model.Colors[i];
                    sumR[bin] += c.r;
                    sumG[bin] += c.g;
                    sumB[bin] += c.b;
                }
            }

            // Revolve: every cell takes its ring's filled/colour verdict.
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                (int a, int p, int q) = Decompose(model, options.Axis, i);
                int bin = a * maxBin + RadiusBin(p - centreP, q - centreQ, maxBin);
                bool ringFilled = total[bin] > 0 && filled[bin] >= options.FillThreshold * total[bin];
                model.Occupied[i] = ringFilled;
                if (ringFilled && filled[bin] > 0)
                {
                    model.Colors[i] = new Color32(
                        (byte)(sumR[bin] / filled[bin]),
                        (byte)(sumG[bin] / filled[bin]),
                        (byte)(sumB[bin] / filled[bin]),
                        255);
                }
            }
        }

        // Clamped to the allocated bin count: the geometry keeps an in-bounds voxel within
        // sqrt(sizeP²+sizeQ²)+1, but the centroid-relative radius sits right at that edge, so the
        // clamp is cheap insurance against float-epsilon rounding tipping past the array bound.
        private static int RadiusBin(float dp, float dq, int maxBin) =>
            Mathf.Min(Mathf.RoundToInt(Mathf.Sqrt(dp * dp + dq * dq)), maxBin - 1);

        private static (int sizeA, int sizeP, int sizeQ) Dimensions(VoxModel model, SymmetryAxis axis) =>
            axis switch
            {
                SymmetryAxis.X => (model.X, model.Y, model.Z),
                SymmetryAxis.Y => (model.Y, model.X, model.Z),
                _ => (model.Z, model.X, model.Y),
            };

        /// <summary>Splits a flat index into its along-axis coordinate and the two perpendicular ones.</summary>
        private static (int a, int p, int q) Decompose(VoxModel model, SymmetryAxis axis, int index)
        {
            (int x, int y, int z) = model.Coords(index);
            return axis switch
            {
                SymmetryAxis.X => (x, y, z),
                SymmetryAxis.Y => (y, x, z),
                _ => (z, x, y),
            };
        }
    }
}
