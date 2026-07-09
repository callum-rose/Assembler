using System;
using NUnit.Framework;

namespace Assembler.AssetGeneration.MeshToVoxel.Editor.Tests
{
    /// <summary>
    /// Pure-logic checks on the two boxiness finishing passes: forced mirror-symmetry (union about
    /// the occupied centre, added voxels copy the mirror's colour, existing ones keep theirs) and
    /// corner fill (an empty cell fills when three same-colour neighbours meet it at a shared vertex,
    /// or when ≥5 neighbours wall it in; straddles, colour boundaries, thin support and gap cells are
    /// left alone).
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
        public void CornerFill_FillsOnThreeSameColourNeighboursSharingAVertex()
        {
            // Centre (1,1,1) has 3 occupied red neighbours on three distinct axes (+X, +Y, +Z) — they
            // meet at a shared vertex, a genuine concave corner → fills red.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 2, 1, 1, Red); // +X
            Set(grid, colours, 1, 2, 1, Red); // +Y
            Set(grid, colours, 1, 1, 2, Red); // +Z

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(1, filled);
            Assert.IsTrue(grid.IsOccupied(1, 1, 1), "3 same-colour neighbours at a shared vertex fill.");
            Assert.AreEqual(Red.r, colours[grid.Index(1, 1, 1)].r, "Filled with the corner colour.");
        }

        [Test]
        public void CornerFill_LeavesSameColourStraddleAlone()
        {
            // The inappropriate-fill case: 3 same-colour neighbours, but +X and −X are opposite (only
            // two axes spanned) so they share no vertex — a straddle across a thin sheet, not a
            // corner. Must stay empty rather than weld across the sheet.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red); // −X
            Set(grid, colours, 2, 1, 1, Red); // +X
            Set(grid, colours, 1, 0, 1, Red); // −Y

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled, "An opposite-pair straddle spans two axes — no shared vertex, no fill.");
            Assert.IsFalse(grid.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_LeavesMixedColourCornerAlone()
        {
            // 3 neighbours forming a geometric corner (+X, +Y, +Z) but no single colour spans all
            // three axes (2 red, 1 blue), and fewer than 5 — must stay empty rather than pick an
            // arbitrary colour at the boundary.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 2, 1, 1, Red);  // +X
            Set(grid, colours, 1, 2, 1, Red);  // +Y
            Set(grid, colours, 1, 1, 2, Blue); // +Z

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled, "No same-colour cluster spans all three axes, and too few neighbours.");
            Assert.IsFalse(grid.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_FillsOnFiveNeighbours_RegardlessOfColour()
        {
            // 5 occupied neighbours with no same-colour corner (3 red, 1 blue, 1 green) — deep enough
            // to box out, fills the modal (red) colour.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);   // −X
            Set(grid, colours, 2, 1, 1, Red);   // +X
            Set(grid, colours, 1, 0, 1, Red);   // −Y
            Set(grid, colours, 1, 2, 1, Blue);  // +Y
            Set(grid, colours, 1, 1, 0, Green); // −Z

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(1, filled, "5 neighbours fills regardless of colour agreement.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 1));
            Assert.AreEqual(Red.r, colours[grid.Index(1, 1, 1)].r, "Filled with the modal (red) colour.");
        }

        [Test]
        public void CornerFill_LeavesFourNeighbourNonCornerAlone()
        {
            // 4 occupied neighbours but no same-colour vertex corner (a red X-straddle plus two
            // differently-coloured Y neighbours) and below the ≥5 threshold — must stay empty.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);   // −X
            Set(grid, colours, 2, 1, 1, Red);   // +X
            Set(grid, colours, 1, 0, 1, Blue);  // −Y
            Set(grid, colours, 1, 2, 1, Green); // +Y

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled, "4 neighbours with no same-colour corner is below the ≥5 fill.");
            Assert.IsFalse(grid.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_ColourTolerance_GroupsNearIdenticalShades()
        {
            // Three near-identical reds (a few RGB units apart) on three distinct axes: exact-match
            // sees three separate colours so no single cluster spans all three axes (no fill), but a
            // small tolerance groups them into one corner cluster that fills.
            var nearReds = new[]
            {
                new Rgba32(240, 40, 40, 255),
                new Rgba32(236, 44, 42, 255),
                new Rgba32(244, 37, 38, 255),
            };

            VoxelGrid exact = Grid(3, 3, 3);
            var exactColours = new Rgba32[exact.Occupied.Length];
            Set(exact, exactColours, 2, 1, 1, nearReds[0]);
            Set(exact, exactColours, 1, 2, 1, nearReds[1]);
            Set(exact, exactColours, 1, 1, 2, nearReds[2]);
            Assert.AreEqual(0, CornerFill.Apply(exact, exactColours, gapFraction: null, colourTolerance: 0f),
                "Exact match sees three distinct shades — no single-colour corner, no fill.");

            VoxelGrid tol = Grid(3, 3, 3);
            var tolColours = new Rgba32[tol.Occupied.Length];
            Set(tol, tolColours, 2, 1, 1, nearReds[0]);
            Set(tol, tolColours, 1, 2, 1, nearReds[1]);
            Set(tol, tolColours, 1, 1, 2, nearReds[2]);
            int filled = CornerFill.Apply(tol, tolColours, gapFraction: null, colourTolerance: 0.1f);

            Assert.AreEqual(1, filled, "A tolerance groups the near-reds into one corner cluster that fills.");
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
            Set(grid, colours, 2, 1, 1, Red); // +X
            Set(grid, colours, 1, 2, 1, Red); // +Y

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(0, filled);
            Assert.IsFalse(grid.IsOccupied(1, 1, 1), "2 neighbours is an edge, not a vertex corner.");
        }

        [Test]
        public void CornerFill_CornerFill_RespectsGapGuard()
        {
            // A same-colour vertex corner that would fill, but the cell is flagged as a real air gap
            // (fine gap fraction > ¼) — the corner fill must leave it open.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 2, 1, 1, Red); // +X
            Set(grid, colours, 1, 2, 1, Red); // +Y
            Set(grid, colours, 1, 1, 2, Red); // +Z

            var gapFraction = new float[grid.Occupied.Length];
            gapFraction[grid.Index(1, 1, 1)] = 1f;

            int filled = CornerFill.Apply(grid, colours, gapFraction);

            Assert.AreEqual(0, filled);
            Assert.IsFalse(grid.IsOccupied(1, 1, 1), "A same-colour corner respects the gap guard.");
        }

        [Test]
        public void CornerFill_DeepConcavity_OverridesGapGuard()
        {
            // The corgi-foot case: an enclosed pocket with 5 occupied neighbours, flagged as an air
            // gap because it is pinched. It is walled in, not a see-through gap, so the ≥5 rule must
            // fill it despite the flag.
            VoxelGrid grid = Grid(3, 3, 3);
            var colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 0, 1, 1, Red);  // −X
            Set(grid, colours, 2, 1, 1, Red);  // +X
            Set(grid, colours, 1, 0, 1, Red);  // −Y
            Set(grid, colours, 1, 2, 1, Blue); // +Y
            Set(grid, colours, 1, 1, 0, Red);  // −Z

            var gapFraction = new float[grid.Occupied.Length];
            gapFraction[grid.Index(1, 1, 1)] = 1f;

            int filled = CornerFill.Apply(grid, colours, gapFraction);

            Assert.AreEqual(1, filled, "A 5-neighbour pocket fills even when gap-flagged.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_RequireMajority_VetoesSplitColourPocket()
        {
            // A 6-neighbour pocket split 3 red / 3 blue: the modal colour is not a strict majority, so
            // requireMajority leaves it open (rather than smearing the seam with an arbitrary side),
            // while turning the veto off fills it with the modal colour.
            static VoxelGrid SplitPocket(out Rgba32[] colours)
            {
                VoxelGrid grid = Grid(3, 3, 3);
                colours = new Rgba32[grid.Occupied.Length];
                Set(grid, colours, 0, 1, 1, Red);  // −X
                Set(grid, colours, 2, 1, 1, Red);  // +X
                Set(grid, colours, 1, 0, 1, Red);  // −Y
                Set(grid, colours, 1, 2, 1, Blue); // +Y
                Set(grid, colours, 1, 1, 0, Blue); // −Z
                Set(grid, colours, 1, 1, 2, Blue); // +Z
                return grid;
            }

            VoxelGrid vetoed = SplitPocket(out Rgba32[] vetoedColours);
            Assert.AreEqual(0, CornerFill.Apply(vetoed, vetoedColours,
                    gapFraction: null, colourTolerance: 0f, neighbourThreshold: 5, requireMajority: true),
                "A 3/3 colour split has no majority — the pocket is left open.");
            Assert.IsFalse(vetoed.IsOccupied(1, 1, 1));

            VoxelGrid filled = SplitPocket(out Rgba32[] filledColours);
            Assert.AreEqual(1, CornerFill.Apply(filled, filledColours,
                    gapFraction: null, colourTolerance: 0f, neighbourThreshold: 5, requireMajority: false),
                "With the veto off the split pocket fills with the modal colour.");
            Assert.IsTrue(filled.IsOccupied(1, 1, 1));
        }

        [Test]
        public void CornerFill_ChasesTheCascade()
        {
            // A=(1,1,1) starts with a full +X/+Y/+Z corner and fills; that makes A the +X neighbour of
            // B=(0,1,1), completing B's corner, so B fills on the next pass. The fill always repeats
            // until stable, so both fill in one call.
            VoxelGrid grid = CascadeGrid(out Rgba32[] colours);

            int filled = CornerFill.Apply(grid, colours, gapFraction: null);

            Assert.AreEqual(2, filled, "The fill chases the cascade: A then B.");
            Assert.IsTrue(grid.IsOccupied(1, 1, 1));
            Assert.IsTrue(grid.IsOccupied(0, 1, 1), "The cascaded corner fills on the second pass.");
        }

        // A=(1,1,1) has a full corner (+X,+Y,+Z) = 3 axes and fills immediately. B=(0,1,1) has +Y,+Z
        // only (2 axes) until A fills and becomes B's +X neighbour, completing B's corner.
        private static VoxelGrid CascadeGrid(out Rgba32[] colours)
        {
            VoxelGrid grid = Grid(3, 3, 3);
            colours = new Rgba32[grid.Occupied.Length];
            Set(grid, colours, 2, 1, 1, Red); // A's +X
            Set(grid, colours, 1, 2, 1, Red); // A's +Y
            Set(grid, colours, 1, 1, 2, Red); // A's +Z
            Set(grid, colours, 0, 2, 1, Red); // B's +Y
            Set(grid, colours, 0, 1, 2, Red); // B's +Z
            return grid;
        }
    }
}
