using System;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Tests
{
    /// <summary>
    /// Pure-logic checks on the two boxiness finishing passes: forced mirror-symmetry (union about
    /// the occupied centre, added voxels copy the mirror's colour, existing ones keep theirs) and
    /// corner fill (empty cell with ≥3 occupied neighbours takes the modal neighbour colour, gap
    /// cells and 2-neighbour cells are left alone).
    /// </summary>
    public sealed class SymmetryCornerFillTests
    {
        private static readonly Color32 Red = new(240, 40, 40, 255);
        private static readonly Color32 Blue = new(40, 40, 240, 255);

        private static VoxelGrid Grid(int nx, int ny, int nz)
        {
            return new VoxelGrid(nx, ny, nz) { Origin = g3.Vector3d.Zero, CellSize = 1.0 };
        }

        private static void Set(VoxelGrid grid, Color32[] colours, int x, int y, int z, Color32 colour)
        {
            int i = grid.Index(x, y, z);
            grid.Occupied[i] = true;
            colours[i] = colour;
        }

        [Test]
        public void Symmetry_None_IsNoOp()
        {
            VoxelGrid grid = Grid(4, 2, 1);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 1, 1, 0, Red);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.None);

            Assert.AreEqual(1, grid.OccupiedCount, "None must not touch the grid.");
        }

        [Test]
        public void Symmetry_X_MirrorsAboutOccupiedCentre_AndCopiesColour()
        {
            // Occupied X extent is [1,3] (centre 2). A lone bump at (1,1) must mirror to (3,1) and
            // copy its colour; the base row is already symmetric.
            VoxelGrid grid = Grid(5, 2, 1);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 1, 0, 0, Blue);
            Set(grid, colours, 2, 0, 0, Blue);
            Set(grid, colours, 3, 0, 0, Blue);
            Set(grid, colours, 1, 1, 0, Red);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.X);

            Assert.IsTrue(grid.IsOccupied(3, 1, 0), "The bump mirrors to the far side.");
            Assert.AreEqual(Red.r, colours[grid.Index(3, 1, 0)].r, "Mirrored voxel copies its source colour.");
            Assert.AreEqual(Red.b, colours[grid.Index(3, 1, 0)].b);
            Assert.IsFalse(grid.IsOccupied(2, 1, 0), "The centre column stays empty.");

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    Assert.AreEqual(grid.IsOccupied(x, y, 0), grid.IsOccupied(4 - x, y, 0),
                        $"Result must be X-symmetric at ({x},{y}).");
                }
            }
        }

        [Test]
        public void Symmetry_ExistingVoxelKeepsOwnColour()
        {
            // Both sides already occupied but with different colours: neither is overwritten.
            VoxelGrid grid = Grid(3, 1, 1);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 0, 0, 0, Red);
            Set(grid, colours, 2, 0, 0, Blue);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.X);

            Assert.AreEqual(Red.r, colours[grid.Index(0, 0, 0)].r, "Left keeps its own colour.");
            Assert.AreEqual(Blue.b, colours[grid.Index(2, 0, 0)].b, "Right keeps its own colour.");
        }

        [Test]
        public void Symmetry_MultipleAxes_SymmetricInEach()
        {
            // Two opposite corners set the occupied extent to [0,2] on both axes (centre (1,1)); X+Y
            // symmetry must then populate all four corners.
            VoxelGrid grid = Grid(3, 3, 1);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 0, 0, 0, Red);
            Set(grid, colours, 2, 2, 0, Blue);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.X | SymmetryAxes.Y);

            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsTrue(grid.IsOccupied(2, 0, 0), "Mirrored in X.");
            Assert.IsTrue(grid.IsOccupied(0, 2, 0), "Mirrored in Y.");
            Assert.IsTrue(grid.IsOccupied(2, 2, 0), "Mirrored in both.");
        }

        [Test]
        public void CornerFill_FillsCellWithThreeNeighbours_TakesModalColour()
        {
            // Centre (1,1,1) empty with 3 occupied face-neighbours: two red, one blue → fills red.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Blue);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(1, filled);
            Assert.IsTrue(grid.IsOccupied(1, 1, 1), "3-neighbour corner fills.");
            Assert.AreEqual(Red.r, colours[grid.Index(1, 1, 1)].r, "Fill takes the modal (red) colour.");
            Assert.AreEqual(Red.b, colours[grid.Index(1, 1, 1)].b);
        }

        [Test]
        public void CornerFill_LeavesTwoNeighbourCellAlone()
        {
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled);
            Assert.IsFalse(grid.IsOccupied(1, 1, 1), "2 neighbours is below the threshold.");
        }

        [Test]
        public void CornerFill_SkipsFlaggedAirGap()
        {
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Color32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Red);

            var gapFraction = new float[grid.Occupied.Length];
            gapFraction[grid.Index(1, 1, 1)] = 1f;

            int filled = CornerFill.Apply(grid, colours, gapFraction);

            Assert.AreEqual(0, filled);
            Assert.IsFalse(grid.IsOccupied(1, 1, 1), "A flagged air gap is never corner-filled.");
        }

        [Test]
        public void CornerFill_IsSinglePass_DoesNotCascade()
        {
            // A 3-long empty channel where only the middle cell starts with 3 neighbours. A single
            // pass fills just that one; it must not then cascade to fill the channel ends.
            VoxelGrid grid = Grid(3, 3, 1);
            var colours = new Color32[grid.Occupied.Length];
            // Fill everything except the middle row's three cells, then knock the row's neighbours
            // so only the centre has 3 occupied neighbours.
            Set(grid, colours, 1, 0, 0, Red); // below centre
            Set(grid, colours, 0, 1, 0, Red); // left of centre
            Set(grid, colours, 2, 1, 0, Red); // right of centre

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(1, filled, "Only the centre qualifies on the snapshot.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 0));
        }
    }
}
