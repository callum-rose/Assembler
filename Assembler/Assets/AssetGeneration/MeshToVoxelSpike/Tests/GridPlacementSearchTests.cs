using System;
using NUnit.Framework;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Tests
{
    /// <summary>
    /// Pure-logic checks on the fine-grid analysis (integrals, thickness, gap mask) and the scored
    /// grid-placement search: the identity candidate reproduces the plain coverage vote, the search
    /// recovers a half-voxel-misaligned box, refuses gap-merging placements, and snaps fractional
    /// extents to whole voxel counts with the stretch reported.
    /// </summary>
    public sealed class GridPlacementSearchTests
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

        private static GridPlacementSearch.Options Options(
            float coverage = 0.5f, bool thinKeep = false, bool scaleFlex = false) => new()
            {
                Coverage = coverage,
                ThinFeatureKeep = thinKeep,
                ScaleFlex = scaleFlex,
                FaceWeight = 1f,
                IouWeight = 1f,
                GapWeight = 2f,
            };

        [Test]
        public void IntegralVolume_BoxCountsMatchBruteForce()
        {
            const int nx = 5, ny = 6, nz = 7;
            var mask = new bool[nx * ny * nz];
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = (i * 2654435761u) % 7 < 3; // deterministic pseudo-pattern
            }

            IntegralVolume integral = IntegralVolume.Build(mask, nx, ny, nz);

            var boxes = new[]
            {
                (0, nx, 0, ny, 0, nz),
                (1, 4, 2, 5, 3, 6),
                (0, 1, 0, 1, 0, 1),
                (4, 5, 5, 6, 6, 7),
                (-2, 3, -1, 9, 2, 99), // deliberately out of range: must clamp
                (3, 3, 0, ny, 0, nz),  // empty box
            };
            foreach ((int x0, int x1, int y0, int y1, int z0, int z1) in boxes)
            {
                int expected = 0;
                for (int z = Math.Max(0, z0); z < Math.Min(nz, z1); z++)
                {
                    for (int y = Math.Max(0, y0); y < Math.Min(ny, y1); y++)
                    {
                        for (int x = Math.Max(0, x0); x < Math.Min(nx, x1); x++)
                        {
                            if (mask[x + nx * (y + ny * z)])
                            {
                                expected++;
                            }
                        }
                    }
                }
                Assert.AreEqual(expected, integral.BoxCount(x0, x1, y0, y1, z0, z1),
                    $"Box ({x0},{x1})×({y0},{y1})×({z0},{z1})");
            }
        }

        [Test]
        public void ThicknessMap_ErodesShellsInward()
        {
            // 5³ solid cube, cap 3: shell = 1, next shell = 2, centre erodes at pass 3.
            VoxelGrid cube = Grid(5, 5, 5, (x, y, z) => true);
            int[] thickness = FineGridAnalysis.ThicknessMap(cube, 3);

            Assert.AreEqual(1, thickness[cube.Index(0, 0, 0)], "Corner is surface.");
            Assert.AreEqual(2, thickness[cube.Index(1, 2, 2)], "Second shell erodes at pass 2.");
            Assert.AreEqual(3, thickness[cube.Index(2, 2, 2)], "Centre is at least cap deep.");

            // A 1-cell-thick plate is surface everywhere.
            VoxelGrid plate = Grid(5, 5, 3, (x, y, z) => z == 1);
            int[] plateThickness = FineGridAnalysis.ThicknessMap(plate, 2);
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    Assert.AreEqual(1, plateThickness[plate.Index(x, y, 1)]);
                }
            }
        }

        [Test]
        public void GapMask_FlagsCellsPinchedBetweenSlabs()
        {
            // Slabs at x=0 and x=3; the empty x=1,2 cells are pinched (opposing occupied ≤2 away).
            VoxelGrid grid = Grid(6, 3, 3, (x, y, z) => x == 0 || x == 3);
            FineGridAnalysis analysis = FineGridAnalysis.Build(grid, 2);

            Assert.IsTrue(analysis.GapMask[grid.Index(1, 1, 1)], "Cell between slabs is a gap.");
            Assert.IsTrue(analysis.GapMask[grid.Index(2, 1, 1)], "Cell between slabs is a gap.");
            Assert.IsFalse(analysis.GapMask[grid.Index(5, 1, 1)], "Open air past the far slab is not a gap.");
        }

        [Test]
        public void Identity_ReproducesPlainCoverageVote()
        {
            VoxelGrid fine = Grid(7, 5, 6, (x, y, z) => ((x * 31 + y * 17 + z * 13) % 5) < 2);
            FineGridAnalysis analysis = FineGridAnalysis.Build(fine, 2);
            GridPlacementSearch.Options options = Options(coverage: 0.5f);

            GridPlacementSearch.Placement placement = GridPlacementSearch.Materialise(
                analysis, GridPlacementSearch.IdentityCandidate(analysis), options);

            VoxelGrid coarse = placement.Grid;
            Assert.AreEqual(4, coarse.NX);
            Assert.AreEqual(3, coarse.NY);
            Assert.AreEqual(3, coarse.NZ);

            for (int oz = 0; oz < coarse.NZ; oz++)
            {
                for (int oy = 0; oy < coarse.NY; oy++)
                {
                    for (int ox = 0; ox < coarse.NX; ox++)
                    {
                        int occ = 0, vol = 0;
                        for (int z = oz * 2; z < Math.Min(fine.NZ, oz * 2 + 2); z++)
                        {
                            for (int y = oy * 2; y < Math.Min(fine.NY, oy * 2 + 2); y++)
                            {
                                for (int x = ox * 2; x < Math.Min(fine.NX, ox * 2 + 2); x++)
                                {
                                    vol++;
                                    if (fine.Occupied[fine.Index(x, y, z)])
                                    {
                                        occ++;
                                    }
                                }
                            }
                        }
                        bool expected = occ > 0 && (float)occ / vol >= 0.5f;
                        Assert.AreEqual(expected, coarse.IsOccupied(ox, oy, oz), $"Block ({ox},{oy},{oz})");
                    }
                }
            }
        }

        [Test]
        public void Search_RecoversHalfVoxelOffsetBoxAlignment()
        {
            // An 8³ box starting at fine (1,1,1) with factor 2: the identity lattice splits it
            // across blocks; the search must find the placement where it fills 4³ blocks exactly.
            VoxelGrid fine = Grid(12, 12, 12, (x, y, z) =>
                x >= 1 && x < 9 && y >= 1 && y < 9 && z >= 1 && z < 9);
            FineGridAnalysis analysis = FineGridAnalysis.Build(fine, 2);
            GridPlacementSearch.Options options = Options(coverage: 0.5f);

            GridPlacementSearch.Placement best = GridPlacementSearch.Run(analysis, options);
            GridPlacementSearch.Placement identity = GridPlacementSearch.Materialise(
                analysis, GridPlacementSearch.IdentityCandidate(analysis), options);

            Assert.AreEqual(1f, best.Score.Face, 1e-4f, "Aligned box has perfect face economy.");
            Assert.AreEqual(1f, best.Score.Iou, 1e-4f, "Aligned box reproduces the fine occupancy exactly.");
            Assert.AreEqual(64, best.Grid.OccupiedCount, "Aligned box is a solid 4³.");
            Assert.Less(identity.Score.Total, best.Score.Total, "The misaligned identity placement must lose.");
        }

        [Test]
        public void Search_KeepsLegGapOpen()
        {
            // Two legs (fine x∈[1,3) and x∈[5,7)) with a 2-cell air gap between: the identity
            // lattice half-covers the gap and merges it; the winner must leave the gap uncovered.
            VoxelGrid fine = Grid(8, 4, 2, (x, y, z) => (x >= 1 && x < 3) || (x >= 5 && x < 7));
            FineGridAnalysis analysis = FineGridAnalysis.Build(fine, 2);
            GridPlacementSearch.Options options = Options(coverage: 0.5f);

            GridPlacementSearch.Placement best = GridPlacementSearch.Run(analysis, options);
            GridPlacementSearch.Placement identity = GridPlacementSearch.Materialise(
                analysis, GridPlacementSearch.IdentityCandidate(analysis), options);

            Assert.AreEqual(1f, best.Score.Gap, 1e-4f, "Winner must not cover any gap cells.");
            Assert.Less(identity.Score.Gap, 1f, "Sanity: the misaligned identity does cover gap cells.");
        }

        [Test]
        public void ScaleFlex_SnapsFractionalExtentToWholeVoxelCount()
        {
            // A 15-fine-cell bar at factor 2 is 7.5 coarse voxels: scale flex must snap to 7 or 8
            // whole voxels and report the stretch. 7 wins on face economy (fewer, chunkier voxels).
            VoxelGrid fine = Grid(15, 2, 2, (x, y, z) => true);
            FineGridAnalysis analysis = FineGridAnalysis.Build(fine, 2);
            GridPlacementSearch.Options options = Options(coverage: 0.5f, scaleFlex: true);

            GridPlacementSearch.Placement best = GridPlacementSearch.Run(analysis, options);

            Assert.AreEqual(7, best.Candidate.Counts.x, "7.5-voxel extent snaps to 7.");
            Assert.AreEqual(15f / 14f, best.Candidate.Scale.x, 1e-3f, "Stretch is reported.");
            Assert.AreEqual(1f, best.Score.Iou, 1e-4f, "The solid bar still reproduces exactly.");
        }

        [Test]
        public void Vote_KeepsConnectedThinLeg_DropsDisconnectedFloater()
        {
            // Solid 4³ body, a 1×1 leg (fine x∈[4,7), y=z=1) attached to it, and a disconnected
            // 1×1 floating bar (x∈[9,12), y=z=3). Thin-keep must force the leg's blocks in (thin AND
            // connected to main) while the floater fails the connectivity gate and the coverage vote.
            VoxelGrid fine = Grid(12, 4, 4, (x, y, z) =>
                (x < 4)
                || (x >= 4 && x < 7 && y == 1 && z == 1)
                || (x >= 9 && y == 3 && z == 3));
            FineGridAnalysis analysis = FineGridAnalysis.Build(fine, 2);
            GridPlacementSearch.Options options = Options(coverage: 0.9f, thinKeep: true);

            GridPlacementSearch.Placement placement = GridPlacementSearch.Materialise(
                analysis, GridPlacementSearch.IdentityCandidate(analysis), options);

            VoxelGrid coarse = placement.Grid;
            Assert.IsTrue(coarse.IsOccupied(0, 0, 0), "Body fills by coverage.");
            Assert.IsTrue(coarse.IsOccupied(2, 0, 0), "Connected thin leg is force-kept.");
            Assert.IsTrue(coarse.IsOccupied(3, 0, 0), "Connected thin leg tip is force-kept.");
            Assert.IsTrue(placement.ThinKept[coarse.Index(2, 0, 0)], "Leg blocks are flagged thin-kept.");
            Assert.IsFalse(coarse.IsOccupied(4, 1, 1), "Disconnected floater fails the connectivity gate.");
            Assert.IsFalse(coarse.IsOccupied(5, 1, 1), "Disconnected floater fails the connectivity gate.");
        }
    }
}
