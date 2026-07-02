using System;
using NUnit.Framework;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Tests
{
    /// <summary>
    /// Pure-logic checks on the coarse-grid cleanup: floater removal keyed off the fine main
    /// component, and the rank close→open morphology — fills a notch, shaves a bump, leaves box
    /// corners alone, never shaves protected thin features, never welds flagged air gaps, and
    /// re-bridges anything it disconnects.
    /// </summary>
    public sealed class OccupancyCleanupTests
    {
        private static VoxelGrid Grid(int nx, int ny, int nz, Func<int, int, int, bool> occupied)
        {
            var grid = new VoxelGrid(nx, ny, nz) { Origin = g3.Vector3d.Zero, CellSize = 1.0 };
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        grid.Occupied[grid.Index(x, y, z)] = occupied(x, y, z);
                    }
                }
            }
            return grid;
        }

        private static bool[] None(VoxelGrid grid) => new bool[grid.Occupied.Length];

        private static float[] NoGaps(VoxelGrid grid) => new float[grid.Occupied.Length];

        [Test]
        public void LabelComponents_CountsAndLabelsDeterministically()
        {
            VoxelGrid grid = Grid(5, 1, 1, (x, y, z) => x != 2);
            int[] labels = OccupancyCleanup.LabelComponents(grid.Occupied, 5, 1, 1, out int count);

            Assert.AreEqual(2, count);
            Assert.AreEqual(0, labels[grid.Index(0, 0, 0)]);
            Assert.AreEqual(0, labels[grid.Index(1, 0, 0)]);
            Assert.AreEqual(-1, labels[grid.Index(2, 0, 0)]);
            Assert.AreEqual(1, labels[grid.Index(3, 0, 0)]);
        }

        [Test]
        public void RemoveFloaters_DropsComponentsWithoutMainSupport()
        {
            // A blob (x<3) and a speck (x=5); only the blob's fine support touches the main component.
            VoxelGrid grid = Grid(7, 2, 2, (x, y, z) => x < 3 || x == 5);
            var touchesMain = new bool[grid.Occupied.Length];
            for (int i = 0; i < grid.Occupied.Length; i++)
            {
                touchesMain[i] = grid.Occupied[i];
            }
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < 2; y++)
                {
                    touchesMain[grid.Index(5, y, z)] = false;
                }
            }

            int removed = OccupancyCleanup.RemoveFloaters(grid, touchesMain);

            Assert.AreEqual(1, removed);
            Assert.IsTrue(grid.IsOccupied(0, 0, 0), "Blob stays.");
            Assert.IsFalse(grid.IsOccupied(5, 0, 0), "Speck dies.");
        }

        [Test]
        public void RemoveFloaters_KeepsLargestWhenNothingTouchesMain()
        {
            VoxelGrid grid = Grid(7, 2, 2, (x, y, z) => x < 3 || x == 5);

            int removed = OccupancyCleanup.RemoveFloaters(grid, new bool[grid.Occupied.Length]);

            Assert.AreEqual(1, removed);
            Assert.IsTrue(grid.IsOccupied(0, 0, 0), "Largest component survives the degenerate case.");
            Assert.IsFalse(grid.IsOccupied(5, 0, 0));
        }

        [Test]
        public void CloseOpen_FillsNotchAndShavesBump_LeavesCornersAlone()
        {
            // A 4³ cube (at [1,5)³ in a 6³ grid) with one face-centre cell missing (notch, 5
            // occupied neighbours) and one cell sticking out of the opposite face (bump, 1 neighbour).
            var notch = (x: 2, y: 2, z: 4);
            var bump = (x: 2, y: 2, z: 0);
            VoxelGrid grid = Grid(6, 6, 6, (x, y, z) =>
                (x >= 1 && x < 5 && y >= 1 && y < 5 && z >= 1 && z < 5 && (x, y, z) != notch)
                || (x, y, z) == bump);

            OccupancyCleanup.CloseOpen(grid, None(grid), NoGaps(grid), strength: 1);

            Assert.IsTrue(grid.IsOccupied(notch.x, notch.y, notch.z), "Notch fills.");
            Assert.IsFalse(grid.IsOccupied(bump.x, bump.y, bump.z), "Bump shaves.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 1), "Cube corner survives (3 neighbours).");
            Assert.IsTrue(grid.IsOccupied(4, 4, 4), "Cube corner survives.");
            Assert.AreEqual(4 * 4 * 4, grid.OccupiedCount, "Result is exactly the solid cube.");
        }

        [Test]
        public void CloseOpen_NeverShavesProtectedThinFeatures()
        {
            // A 4³ body with a 3-long 1×1 leg: unprotected, strength 2 eats the whole leg (tip has
            // 1 neighbour, the rest 2); protected, it must survive untouched.
            static VoxelGrid Build() => Grid(9, 4, 4, (x, y, z) =>
                x < 4 || (x < 7 && y == 1 && z == 1));

            VoxelGrid unprotected = Build();
            OccupancyCleanup.CloseOpen(unprotected, None(unprotected), NoGaps(unprotected), strength: 1);
            Assert.IsFalse(unprotected.IsOccupied(6, 1, 1), "Sanity: the unprotected leg tip shaves.");

            VoxelGrid guarded = Build();
            bool[] thinKept = None(guarded);
            for (int x = 4; x < 7; x++)
            {
                thinKept[guarded.Index(x, 1, 1)] = true;
            }
            bool[] protectedMask = OccupancyCleanup.BuildProtectedMask(thinKept, 9, 4, 4);

            OccupancyCleanup.CloseOpen(guarded, protectedMask, NoGaps(guarded), strength: 2);

            Assert.IsTrue(guarded.IsOccupied(4, 1, 1), "Protected leg survives.");
            Assert.IsTrue(guarded.IsOccupied(5, 1, 1), "Protected leg survives.");
            Assert.IsTrue(guarded.IsOccupied(6, 1, 1), "Protected leg tip survives.");
        }

        [Test]
        public void CloseOpen_GapGuardBlocksWeldingFlaggedCells()
        {
            // A 3³ solid with a hollow centre (6 occupied neighbours — prime close bait). With the
            // centre flagged as an air gap it must stay open; unflagged it welds shut.
            static VoxelGrid Build() => Grid(3, 3, 3, (x, y, z) => (x, y, z) != (1, 1, 1));

            VoxelGrid unflagged = Build();
            OccupancyCleanup.CloseOpen(unflagged, None(unflagged), NoGaps(unflagged), strength: 1);
            Assert.IsTrue(unflagged.IsOccupied(1, 1, 1), "Sanity: an unflagged cavity welds shut.");

            VoxelGrid flagged = Build();
            float[] gapFraction = NoGaps(flagged);
            gapFraction[flagged.Index(1, 1, 1)] = 1f;
            OccupancyCleanup.CloseOpen(flagged, None(flagged), gapFraction, strength: 2);
            Assert.IsFalse(flagged.IsOccupied(1, 1, 1), "The flagged air gap stays open.");
        }

        [Test]
        public void CloseOpen_ReconnectsWhatItDisconnected()
        {
            // Two 3³ blobs joined by an unprotected 2-long 1×1 bridge. Strength 2's second pass
            // shaves the bridge (2 neighbours each) and splits the model; the reconnect net must
            // restore a path so the result is one component again.
            VoxelGrid grid = Grid(8, 3, 3, (x, y, z) => x < 3 || x >= 5 || (y == 1 && z == 1));

            OccupancyCleanup.CloseOpen(grid, None(grid), NoGaps(grid), strength: 2);

            OccupancyCleanup.LabelComponents(grid.Occupied, 8, 3, 3, out int components);
            Assert.AreEqual(1, components, "Reconnect net must re-bridge the split.");
            Assert.IsTrue(grid.IsOccupied(0, 0, 0), "Blob intact.");
            Assert.IsTrue(grid.IsOccupied(7, 2, 2), "Blob intact.");
        }
    }
}
