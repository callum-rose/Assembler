using UnityEngine;

namespace Assembler.VoxelPipeline
{
    /// <summary>
    /// Pipeline step 6 (last) — mild geometric despeckle/fill. Colour anomalies are already
    /// handled by de-light + palette-snap; what remains is geometric noise:
    ///   - remove isolated protruding bumps (a filled voxel with too few occupied neighbours);
    ///   - fill single-voxel pinholes (an empty voxel almost fully enclosed by occupied ones).
    ///
    /// Kept <b>mild and opt-in</b> — aggressive morphology erodes intended thin features (legs,
    /// antennae). Both passes read a single snapshot so edits don't cascade within one call.
    /// </summary>
    public static class Morphology
    {
        public readonly struct Options
        {
            /// <summary>A filled voxel with this many or fewer occupied face-neighbours is removed.</summary>
            public int RemoveAtOrBelowNeighbours { get; }

            /// <summary>An empty voxel with this many or more occupied face-neighbours is filled.</summary>
            public int FillAtOrAboveNeighbours { get; }

            public Options(int removeAtOrBelowNeighbours, int fillAtOrAboveNeighbours)
            {
                RemoveAtOrBelowNeighbours = removeAtOrBelowNeighbours;
                FillAtOrAboveNeighbours = fillAtOrAboveNeighbours;
            }

            // Mild: drop voxels clinging by a single face; fill cells enclosed on 5+ of 6 faces.
            public static Options Default => new Options(1, 5);
        }

        public static void Apply(VoxModel model, Options options)
        {
            bool[] before = (bool[])model.Occupied.Clone();
            var colorsBefore = (Color32[])model.Colors.Clone();

            for (int i = 0; i < before.Length; i++)
            {
                (int x, int y, int z) = model.Coords(i);
                int occupiedNeighbours = CountOccupiedNeighbours(model, before, x, y, z);

                if (before[i])
                {
                    if (occupiedNeighbours <= options.RemoveAtOrBelowNeighbours)
                    {
                        model.Occupied[i] = false;
                    }
                }
                else if (occupiedNeighbours >= options.FillAtOrAboveNeighbours)
                {
                    model.Occupied[i] = true;
                    model.Colors[i] = MajorityNeighbourColor(model, before, colorsBefore, x, y, z);
                }
            }
        }

        private static int CountOccupiedNeighbours(VoxModel model, bool[] before, int x, int y, int z)
        {
            int count = 0;
            foreach ((int dx, int dy, int dz) in VoxModel.FaceNeighbours)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (model.InBounds(nx, ny, nz) && before[model.Index(nx, ny, nz)])
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Most common colour among a filled cell's occupied neighbours (for pinhole fills).</summary>
        private static Color32 MajorityNeighbourColor(
            VoxModel model, bool[] before, Color32[] colorsBefore, int x, int y, int z)
        {
            Color32 best = default;
            int bestKey = -1;
            int bestCount = 0;
            foreach ((int dx, int dy, int dz) in VoxModel.FaceNeighbours)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (!model.InBounds(nx, ny, nz))
                {
                    continue;
                }
                int n = model.Index(nx, ny, nz);
                if (!before[n])
                {
                    continue;
                }

                Color32 c = colorsBefore[n];
                int key = (c.r << 16) | (c.g << 8) | c.b;
                int count = 0;
                foreach ((int ex, int ey, int ez) in VoxModel.FaceNeighbours)
                {
                    int mx = x + ex, my = y + ey, mz = z + ez;
                    if (!model.InBounds(mx, my, mz))
                    {
                        continue;
                    }
                    int m = model.Index(mx, my, mz);
                    Color32 oc = colorsBefore[m];
                    if (before[m] && ((oc.r << 16) | (oc.g << 8) | oc.b) == key)
                    {
                        count++;
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestKey = key;
                    best = c;
                }
            }
            return bestKey >= 0 ? best : default;
        }
    }
}
