using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Occupancy-grid cleanup passes that run on the coarse grid after the placement vote:
    /// floater removal (drop components whose fine support never touches the fine main component)
    /// and a protected morphological close→open (fill one-voxel notches, shave lone bumps) that is
    /// forbidden from eroding protected thin features, from closing real air gaps, and — via a
    /// post-pass reconnect net — from splitting the model apart. Also hosts the shared 6-connected
    /// component labelling the fine-grid analysis reuses.
    /// </summary>
    public static class OccupancyCleanup
    {
        private static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
        };

        /// <summary>
        /// Label 6-connected components of <paramref name="occupied"/> (layout as
        /// <see cref="VoxelGrid.Index"/>). Returns per-cell labels (−1 = empty), labels assigned in
        /// deterministic scan order starting at 0.
        /// </summary>
        public static int[] LabelComponents(bool[] occupied, int nx, int ny, int nz, out int componentCount)
        {
            var labels = new int[occupied.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = -1;
            }

            componentCount = 0;
            var queue = new Queue<(int x, int y, int z)>();

            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        int seed = x + nx * (y + ny * z);
                        if (!occupied[seed] || labels[seed] >= 0)
                        {
                            continue;
                        }

                        int label = componentCount++;
                        labels[seed] = label;
                        queue.Enqueue((x, y, z));
                        while (queue.Count > 0)
                        {
                            (int cx, int cy, int cz) = queue.Dequeue();
                            foreach ((int dx, int dy, int dz) in FaceNeighbours)
                            {
                                int px = cx + dx, py = cy + dy, pz = cz + dz;
                                if (px < 0 || px >= nx || py < 0 || py >= ny || pz < 0 || pz >= nz)
                                {
                                    continue;
                                }
                                int i = px + nx * (py + ny * pz);
                                if (occupied[i] && labels[i] < 0)
                                {
                                    labels[i] = label;
                                    queue.Enqueue((px, py, pz));
                                }
                            }
                        }
                    }
                }
            }
            return labels;
        }

        /// <summary>
        /// Drop coarse components with no cell whose fine support intersects the fine main
        /// component (<paramref name="touchesMain"/>, from the placement vote). If nothing touches
        /// main — degenerate, e.g. an empty fine grid — the largest component is kept so the model
        /// never vanishes. Mutates <paramref name="grid"/>; returns the number of components removed.
        /// </summary>
        public static int RemoveFloaters(VoxelGrid grid, bool[] touchesMain)
        {
            int[] labels = LabelComponents(grid.Occupied, grid.NX, grid.NY, grid.NZ, out int count);
            if (count <= 1)
            {
                return 0;
            }

            var keep = new bool[count];
            var sizes = new int[count];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] < 0)
                {
                    continue;
                }
                sizes[labels[i]]++;
                if (touchesMain[i])
                {
                    keep[labels[i]] = true;
                }
            }

            bool anyKept = false;
            foreach (bool k in keep)
            {
                anyKept |= k;
            }
            if (!anyKept)
            {
                int largest = 0;
                for (int c = 1; c < count; c++)
                {
                    if (sizes[c] > sizes[largest])
                    {
                        largest = c;
                    }
                }
                keep[largest] = true;
            }

            int removed = 0;
            foreach (bool k in keep)
            {
                if (!k)
                {
                    removed++;
                }
            }
            if (removed == 0)
            {
                return 0;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] >= 0 && !keep[labels[i]])
                {
                    grid.Occupied[i] = false;
                }
            }
            return removed;
        }

        // A coarse cell whose fine support is more than a quarter air-gap is a real gap — close
        // must never weld it shut.
        private const float GapFillLimit = 0.25f;

        /// <summary>
        /// Morphological close→open at <paramref name="strength"/> (0 = off, 1–2), in the
        /// rank/majority generalisation rather than the classic structuring-element form: close
        /// fills empty cells with ≥5 occupied face-neighbours (≥4 on the second pass) — pits and
        /// notches — and open shaves occupied cells with ≤1 occupied face-neighbours (≤2 on the
        /// second pass) — lone bumps and spikes. A classic SE-based close→open would chamfer every
        /// box edge and corner (erode a cube by the 6-cross and dilate back: the 12 edges never
        /// return), the opposite of the crisp Crossy-Road look; rank thresholds leave corners
        /// (3 neighbours) and edges (4) alone. Close never fills cells whose fine gap fraction
        /// exceeds ¼; open never shaves <paramref name="protectedMask"/> cells (thin-kept blocks
        /// dilated by 1); a reconnect net then bridges any components the pass split apart.
        /// Mutates <paramref name="grid"/>.
        /// </summary>
        public static void CloseOpen(VoxelGrid grid, bool[] protectedMask, float[] gapFraction, int strength)
        {
            int passes = Mathf.Clamp(strength, 0, 2);
            if (passes == 0)
            {
                return;
            }

            var before = (bool[])grid.Occupied.Clone();
            for (int pass = 1; pass <= passes; pass++)
            {
                RankClose(grid, gapFraction, minNeighbours: pass == 1 ? 5 : 4);
                RankOpen(grid, protectedMask, maxNeighbours: pass == 1 ? 1 : 2);
            }
            Reconnect(grid, before);
        }

        /// <summary>Thin-kept blocks dilated by 1 — the cells morphology must never shave.</summary>
        public static bool[] BuildProtectedMask(bool[] thinKept, int nx, int ny, int nz)
        {
            var mask = (bool[])thinKept.Clone();
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        int i = x + nx * (y + ny * z);
                        if (!thinKept[i] && CountNeighbours(thinKept, nx, ny, nz, x, y, z) > 0)
                        {
                            mask[i] = true;
                        }
                    }
                }
            }
            return mask;
        }

        // ---- Rank morphology (simultaneous update against a snapshot, so order can't matter) ----

        private static void RankClose(VoxelGrid grid, float[] gapFraction, int minNeighbours)
        {
            bool[] occ = grid.Occupied;
            var snapshot = (bool[])occ.Clone();
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (snapshot[i] || gapFraction[i] > GapFillLimit)
                        {
                            continue;
                        }
                        if (CountNeighbours(snapshot, grid.NX, grid.NY, grid.NZ, x, y, z) >= minNeighbours)
                        {
                            occ[i] = true;
                        }
                    }
                }
            }
        }

        private static void RankOpen(VoxelGrid grid, bool[] protectedMask, int maxNeighbours)
        {
            bool[] occ = grid.Occupied;
            var snapshot = (bool[])occ.Clone();
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (!snapshot[i] || protectedMask[i])
                        {
                            continue;
                        }
                        if (CountNeighbours(snapshot, grid.NX, grid.NY, grid.NZ, x, y, z) <= maxNeighbours)
                        {
                            occ[i] = false;
                        }
                    }
                }
            }
        }

        // Off-grid counts as empty, matching the thickness map's convention.
        private static int CountNeighbours(bool[] occ, int nx, int ny, int nz, int x, int y, int z)
        {
            int count = 0;
            foreach ((int dx, int dy, int dz) in FaceNeighbours)
            {
                int px = x + dx, py = y + dy, pz = z + dz;
                if (px >= 0 && px < nx && py >= 0 && py < ny && pz >= 0 && pz < nz
                    && occ[px + nx * (py + ny * pz)])
                {
                    count++;
                }
            }
            return count;
        }

        // ---- Reconnect safety net -------------------------------------------

        // If close→open split the grid into pieces that were connected beforehand, restore a
        // minimal bridge per stranded component: BFS from the stranded cells through the union of
        // the pre-morphology and current occupancy until the main component is reached, then re-add
        // the path. Deterministic (index-order BFS). Pieces that were already separate before the
        // pass (they survived floater removal on purpose) are left alone.
        private static void Reconnect(VoxelGrid grid, bool[] before)
        {
            int nx = grid.NX, ny = grid.NY, nz = grid.NZ;
            bool[] occ = grid.Occupied;

            int[] labels = LabelComponents(occ, nx, ny, nz, out int count);
            if (count <= 1)
            {
                return;
            }

            var sizes = new int[count];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] >= 0)
                {
                    sizes[labels[i]]++;
                }
            }
            int main = 0;
            for (int c = 1; c < count; c++)
            {
                if (sizes[c] > sizes[main])
                {
                    main = c;
                }
            }

            var union = new bool[occ.Length];
            for (int i = 0; i < occ.Length; i++)
            {
                union[i] = occ[i] || before[i];
            }

            for (int c = 0; c < count; c++)
            {
                if (c == main)
                {
                    continue;
                }
                BridgeToMain(occ, labels, union, c, main, nx, ny, nz);
                // Re-adding a bridge can only merge components; labels for other components stay valid.
            }
        }

        private static void BridgeToMain(bool[] occ, int[] labels, bool[] union, int from, int main, int nx, int ny, int nz)
        {
            var parent = new int[occ.Length];
            var visited = new bool[occ.Length];
            var queue = new Queue<int>();

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == from)
                {
                    visited[i] = true;
                    parent[i] = -1;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int z = i / (nx * ny);
                int rem = i - z * nx * ny;
                int y = rem / nx;
                int x = rem - y * nx;

                foreach ((int dx, int dy, int dz) in FaceNeighbours)
                {
                    int px = x + dx, py = y + dy, pz = z + dz;
                    if (px < 0 || px >= nx || py < 0 || py >= ny || pz < 0 || pz >= nz)
                    {
                        continue;
                    }
                    int n = px + nx * (py + ny * pz);
                    if (visited[n] || !union[n])
                    {
                        continue;
                    }

                    visited[n] = true;
                    parent[n] = i;
                    if (labels[n] == main)
                    {
                        // Walk the path back, restoring every cell that isn't already occupied.
                        for (int cell = n; cell >= 0; cell = parent[cell])
                        {
                            occ[cell] = true;
                        }
                        return;
                    }
                    queue.Enqueue(n);
                }
            }
            // No route even through the pre-morphology cells: the piece was never connected — leave it.
        }
    }
}
