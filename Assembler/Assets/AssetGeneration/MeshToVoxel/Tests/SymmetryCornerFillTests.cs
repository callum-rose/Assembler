using System;
using NUnit.Framework;
using Assembler.AssetGeneration.Colour;

namespace Assembler.AssetGeneration.MeshToVoxel.Editor.Tests
{
    /// <summary>
    /// Pure-logic checks on the two boxiness finishing passes: forced mirror-symmetry (union about
    /// the occupied centre, added voxels copy the mirror's colour, existing ones keep theirs) and
    /// corner fill (empty cell with ≥3 occupied neighbours takes the modal neighbour colour, gap
    /// cells and 2-neighbour cells are left alone).
    /// </summary>
    public sealed class SymmetryCornerFillTests
    {
        private static readonly Rgba32 Red = new(240, 40, 40, 255);
        private static readonly Rgba32 Blue = new(40, 40, 240, 255);
        private static readonly Rgba32 Green = new(40, 200, 40, 255);

        private static VoxelGrid Grid(int nx, int ny, int nz)
        {
            return new VoxelGrid(nx, ny, nz) { Origin = g3.Vector3d.Zero, CellSize = 1.0 };
        }

        private static void Set(VoxelGrid grid, Rgba32[] colours, int x, int y, int z, Rgba32 colour)
        {
            int i = grid.Index(x, y, z);
            grid.Occupied[i] = true;
            colours[i] = colour;
        }

        [Test]
        public void Symmetry_None_IsNoOp()
        {
            VoxelGrid grid = Grid(4, 2, 1);
            var colours = new Rgba32[grid.Occupied.Length];
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
            var colours = new Rgba32[grid.Occupied.Length];
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
            var colours = new Rgba32[grid.Occupied.Length];
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
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 0, 0, Red);
            Set(grid, colours, 2, 2, 0, Blue);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.X | SymmetryAxes.Y);

            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsTrue(grid.IsOccupied(2, 0, 0), "Mirrored in X.");
            Assert.IsTrue(grid.IsOccupied(0, 2, 0), "Mirrored in Y.");
            Assert.IsTrue(grid.IsOccupied(2, 2, 0), "Mirrored in both.");
        }

        [Test]
        public void ForceMirror_ReflectsDominantHalf_OverridingTheOther()
        {
            // Occupied X extent [0,4] (centre 2). Low half {0,1} has 2 voxels, high half {3,4} has 1,
            // and (4) is a wrong Blue. Force-mirror must reflect the low half over the high half:
            // (3)&(4) become Red, the Blue is overridden, and the result is an exact mirror.
            VoxelGrid grid = Grid(5, 1, 1);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 0, 0, Red);
            Set(grid, colours, 1, 0, 0, Red);
            Set(grid, colours, 4, 0, 0, Blue);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.X, forceMirror: true);

            Assert.IsTrue(grid.IsOccupied(3, 0, 0), "Low half reflected onto (3).");
            Assert.IsTrue(grid.IsOccupied(4, 0, 0));
            Assert.AreEqual(Red.r, colours[grid.Index(4, 0, 0)].r, "The wrong Blue is overridden by the mirror.");
            Assert.AreEqual(Red.b, colours[grid.Index(4, 0, 0)].b);
            Assert.IsFalse(grid.IsOccupied(2, 0, 0), "Centre stays empty.");

            for (int x = 0; x < 5; x++)
            {
                Assert.AreEqual(grid.IsOccupied(x, 0, 0), grid.IsOccupied(4 - x, 0, 0),
                    $"Geometry must be an exact mirror at x={x}.");
                if (grid.IsOccupied(x, 0, 0))
                {
                    Assert.AreEqual(colours[grid.Index(x, 0, 0)].r, colours[grid.Index(4 - x, 0, 0)].r,
                        $"Colour must be an exact mirror at x={x}.");
                }
            }
        }

        [Test]
        public void ForceMirror_KeepsTheHigherCountSide()
        {
            // Mirror image of the above: the high half {3,4} is dominant, so it is reflected onto the
            // low half and (0)'s wrong Blue is overridden.
            VoxelGrid grid = Grid(5, 1, 1);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 0, 0, Blue);
            Set(grid, colours, 3, 0, 0, Red);
            Set(grid, colours, 4, 0, 0, Red);

            SymmetryEnforcer.Apply(grid, colours, SymmetryAxes.X, forceMirror: true);

            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsTrue(grid.IsOccupied(1, 0, 0), "High half reflected onto (1).");
            Assert.AreEqual(Red.r, colours[grid.Index(0, 0, 0)].r, "The wrong Blue is overridden.");
        }

        [Test]
        public void CornerFill_FillsOnThreeSameColourNeighbours()
        {
            // Centre (1,1,1) has 3 occupied neighbours all red → clear colour consensus, fills red.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Red);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(1, filled);
            Assert.IsTrue(grid.IsOccupied(1, 1, 1), "3 same-colour neighbours fill.");
            Assert.AreEqual(Red.r, colours[grid.Index(1, 1, 1)].r, "Filled with the consensus colour.");
        }

        [Test]
        public void CornerFill_LeavesMixedThreeNeighbourCornerAlone()
        {
            // The artifact case: 3 neighbours but no colour consensus (2 red, 1 blue) and fewer than
            // 4 — must stay empty rather than fill with an arbitrary modal pick at the boundary.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Blue);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled, "A mixed 3-neighbour corner has no consensus and too few neighbours.");
            Assert.IsFalse(grid.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_FillsOnFourNeighbours_RegardlessOfColour()
        {
            // 4 occupied neighbours with no 3-consensus (2 red, 1 blue, 1 green) — deep enough to box
            // out, fills the modal (red) colour.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Blue);
            Set(grid, colours, 1, 2, 1, Green);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(1, filled, "4 neighbours fills regardless of colour agreement.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 1));
            Assert.AreEqual(Red.r, colours[grid.Index(1, 1, 1)].r, "Filled with the modal (red) colour.");
        }

        [Test]
        public void CornerFill_ColourTolerance_GroupsNearIdenticalShades()
        {
            // Three near-identical reds (a few RGB units apart): exact-match sees three separate
            // colours and won't fill (no ≥3 consensus, only 3 neighbours), but a small tolerance
            // groups them into one consensus that fills.
            var nearReds = new[]
            {
                new Rgba32(240, 40, 40, 255),
                new Rgba32(236, 44, 42, 255),
                new Rgba32(244, 37, 38, 255),
            };

            VoxelGrid exact = Grid(3, 3, 3);
            var exactColours = new Rgba32[exact.Occupied.Length];
            Set(exact, exactColours, 0, 1, 1, nearReds[0]);
            Set(exact, exactColours, 2, 1, 1, nearReds[1]);
            Set(exact, exactColours, 1, 0, 1, nearReds[2]);
            Assert.AreEqual(0, CornerFill.Apply(exact, exactColours, gapFraction: null, recursive: false, colourTolerance: 0f),
                "Exact match sees three distinct shades — no consensus, no fill.");

            VoxelGrid tol = Grid(3, 3, 3);
            var tolColours = new Rgba32[tol.Occupied.Length];
            Set(tol, tolColours, 0, 1, 1, nearReds[0]);
            Set(tol, tolColours, 2, 1, 1, nearReds[1]);
            Set(tol, tolColours, 1, 0, 1, nearReds[2]);
            int filled = CornerFill.Apply(tol, tolColours, gapFraction: null, recursive: false, colourTolerance: 0.1f);

            Assert.AreEqual(1, filled, "A tolerance groups the near-reds into a consensus that fills.");
            Assert.IsTrue(tol.IsOccupied(1, 1, 1));
            Rgba32 fill = tolColours[tol.Index(1, 1, 1)];
            Assert.IsTrue(System.Array.Exists(nearReds, c => c.r == fill.r && c.g == fill.g && c.b == fill.b),
                "Filled with one of the clustered near-red neighbour colours.");
        }

        [Test]
        public void CornerFill_LeavesTwoNeighbourCellAlone()
        {
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled);
            Assert.IsFalse(grid.IsOccupied(1, 1, 1), "2 neighbours is below both thresholds.");
        }

        [Test]
        public void CornerFill_ConsensusFill_RespectsGapGuard()
        {
            // A 3-same-colour consensus that would fill, but the cell is flagged as a real air gap
            // (fine gap fraction > ¼) — the shallow consensus fill must leave it open.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Red);

            var gapFraction = new float[grid.Occupied.Length];
            gapFraction[grid.Index(1, 1, 1)] = 1f;

            int filled = CornerFill.Apply(grid, colours, gapFraction);

            Assert.AreEqual(0, filled);
            Assert.IsFalse(grid.IsOccupied(1, 1, 1), "A 3-neighbour consensus respects the gap guard.");
        }

        [Test]
        public void CornerFill_DeepConcavity_OverridesGapGuard()
        {
            // The corgi-foot case: an enclosed pocket with 4+ occupied neighbours, flagged as an air
            // gap because it is pinched on two axes. It is walled in, not a see-through gap, so the
            // ≥4 rule must fill it despite the flag.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);
            Set(grid, colours, 2, 1, 1, Red);
            Set(grid, colours, 1, 0, 1, Red);
            Set(grid, colours, 1, 2, 1, Blue);

            var gapFraction = new float[grid.Occupied.Length];
            gapFraction[grid.Index(1, 1, 1)] = 1f;

            int filled = CornerFill.Apply(grid, colours, gapFraction);

            Assert.AreEqual(1, filled, "A 4-neighbour pocket fills even when gap-flagged.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_SinglePass_DoesNotCascade()
        {
            // (1,1) starts with 3 occupied neighbours; (2,1) starts with only 2 and would gain a
            // third only after (1,1) fills. A single pass fills (1,1) but must leave (2,1) empty.
            VoxelGrid grid = CascadeGrid(out Rgba32[] colours);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null, recursive: false);

            Assert.AreEqual(1, filled, "Only (1,1) qualifies on the first snapshot.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 0));
            Assert.IsFalse(grid.IsOccupied(2, 1, 0), "Single pass must not cascade into (2,1).");
        }

        [Test]
        public void CornerFill_Recursive_ChasesTheCascade()
        {
            // Same setup: recursive keeps going, so filling (1,1) gives (2,1) its third neighbour
            // and it fills on the next pass.
            VoxelGrid grid = CascadeGrid(out Rgba32[] colours);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null, recursive: true);

            Assert.AreEqual(2, filled, "Recursion fills (1,1) then (2,1).");
            Assert.IsTrue(grid.IsOccupied(1, 1, 0));
            Assert.IsTrue(grid.IsOccupied(2, 1, 0), "The cascaded corner fills on the second pass.");
        }

        // (1,1) has neighbours (0,1),(1,0),(1,2) occupied = 3; (2,1) has (2,0),(2,2) = 2 until (1,1) fills.
        private static VoxelGrid CascadeGrid(out Rgba32[] colours)
        {
            VoxelGrid grid = Grid(3, 3, 1);
            colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 0, Red);
            Set(grid, colours, 1, 0, 0, Red);
            Set(grid, colours, 1, 2, 0, Red);
            Set(grid, colours, 2, 0, 0, Red);
            Set(grid, colours, 2, 2, 0, Red);
            return grid;
        }
    }
}
