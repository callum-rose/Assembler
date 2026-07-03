using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Tests
{
    /// <summary>
    /// Checks on the colour passes — UV island dilation floods gutter purple away, the Oklab medoid
    /// rejects a minority outlier, Potts smoothing flips speckle but pins real edges — plus the
    /// metrics counts and a full-pipeline smoke test on a temp-file cube.
    /// </summary>
    public sealed class SpikeColourPassTests
    {
        private static readonly Rgba32 Red = new(240, 40, 40, 255);
        private static readonly Rgba32 Blue = new(40, 40, 240, 255);
        private static readonly Rgba32 Purple = new(255, 0, 255, 255);

        // One triangle covering the lower-left UV half (u + v ≤ 1), built the same way the OBJ
        // importer builds meshes (EnableVertexUVs + NewVertexInfo).
        private static g3.DMesh3 UvTriangleMesh()
        {
            var mesh = new g3.DMesh3();
            mesh.EnableVertexUVs(g3.Vector2f.Zero);
            var corners = new (g3.Vector3d pos, g3.Vector2f uv)[]
            {
                (new g3.Vector3d(0, 0, 0), new g3.Vector2f(0f, 0f)),
                (new g3.Vector3d(1, 0, 0), new g3.Vector2f(1f, 0f)),
                (new g3.Vector3d(0, 1, 0), new g3.Vector2f(0f, 1f)),
            };
            foreach ((g3.Vector3d pos, g3.Vector2f uv) in corners)
            {
                var info = new g3.NewVertexInfo(pos) { bHaveUV = true, uv = uv };
                mesh.AppendVertex(ref info);
            }
            mesh.AppendTriangle(0, 1, 2);
            return mesh;
        }

        [Test]
        public void UvIslandDilation_RasterisesTexelCentresInsideUvTriangles()
        {
            bool[] covered = UvIslandDilation.RasteriseCoverage(UvTriangleMesh(), 8, 8);

            Assert.IsTrue(covered[1 + 8 * 1], "Texel well inside the island is covered.");
            Assert.IsTrue(covered[5 + 8 * 0], "Texel along the island's bottom edge is covered.");
            Assert.IsFalse(covered[7 + 8 * 7], "Texel deep in the gutter is not covered.");
        }

        [Test]
        public void UvIslandDilation_FloodsIslandColourOverGutterPurple()
        {
            // Red island (over-painted past the coverage boundary so no covered texel is purple),
            // purple gutter. After dilation a sample in the far gutter corner must read red.
            const int size = 8;
            var pixels = new ColorF[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[x + size * y] = x + y <= size ? (ColorF)Red : (ColorF)Purple;
                }
            }
            var model = new LoadedModel(
                UvTriangleMesh(), hasUVs: true,
                new ColorSource
                {
                    Texture = new TextureSnapshot(pixels, size, size),
                });

            LoadedModel dilated = UvIslandDilation.Apply(model, UvIslandDilation.DefaultPasses);

            Assert.AreNotSame(model, dilated);
            ColorF corner = dilated.Colors.Texture!.SampleBilinear(7.5f / size, 7.5f / size);
            Assert.Less(corner.b, 0.2f, "Gutter purple must be flooded away (blue channel gone).");
            Assert.Greater(corner.r, 0.5f, "Gutter takes the island's red.");
        }

        [Test]
        public void OklabMedoid_RejectsMinorityOutlier()
        {
            var samples = new List<Rgba32>();
            for (int i = 0; i < 8; i++)
            {
                samples.Add(Red);
            }
            samples.Insert(4, Purple); // the 1-in-9 purple

            Rgba32 medoid = MultiSampleColour.OklabMedoid(samples);

            Assert.AreEqual(Red.r, medoid.r);
            Assert.AreEqual(Red.g, medoid.g);
            Assert.AreEqual(Red.b, medoid.b);
        }

        [Test]
        public void Consolidate_MergesNearShadesButKeepsDistinctColours()
        {
            // Two near-identical reds, one near-identical blue, one distinct blue. A generous
            // tolerance folds the reds together and the blues together → two fundamental colours.
            var colours = new[]
            {
                new Rgba32(240, 40, 40, 255),
                new Rgba32(236, 44, 42, 255), // ~red
                new Rgba32(40, 40, 240, 255),
                new Rgba32(44, 38, 236, 255), // ~blue
            };

            ColourModes.PaletteAssignment merged = ColourModes.AssignPalette(
                colours, mask: null, ColourMode.Consolidated,
                new ColourModes.Options { ConsolidateTolerance = 0.1f });

            Assert.IsNotNull(merged.Palette);
            Assert.AreEqual(2, merged.Palette!.Length, "Near shades collapse to two fundamental colours.");
            Assert.AreEqual(merged.Colours[0], merged.Colours[1], "The two reds share one representative.");
            Assert.AreEqual(merged.Colours[2], merged.Colours[3], "The two blues share one representative.");
            Assert.AreNotEqual(merged.Colours[0], merged.Colours[2], "Red and blue stay distinct.");
        }

        [Test]
        public void Consolidate_ZeroTolerance_KeepsEveryDistinctColour()
        {
            var colours = new[] { Red, Blue, new Rgba32(241, 41, 41, 255) };

            ColourModes.PaletteAssignment merged = ColourModes.AssignPalette(
                colours, mask: null, ColourMode.Consolidated,
                new ColourModes.Options { ConsolidateTolerance = 0f });

            Assert.AreEqual(3, merged.Palette!.Length, "Exact merging leaves every distinct colour intact.");
        }

        [Test]
        public void Consolidate_MaxColours_LocksToTheCapAndKeepsDominantColours()
        {
            // Five well-separated colours, zero tolerance (nothing merges on distance). The cap must
            // agglomerate down to exactly three, and — merging nearest pairs — the two orange-ish
            // shades collapse while the dominant red/green/blue endpoints survive.
            var green = new Rgba32(40, 200, 40, 255);
            var orange = new Rgba32(230, 140, 20, 255);
            var orange2 = new Rgba32(210, 120, 30, 255);
            var colours = new[] { Red, green, Blue, orange, orange2 };

            ColourModes.PaletteAssignment merged = ColourModes.AssignPalette(
                colours, mask: null, ColourMode.Consolidated,
                new ColourModes.Options { ConsolidateTolerance = 0f, ConsolidateMaxColours = 3 });

            Assert.AreEqual(3, merged.Palette!.Length, "The cap locks the output to three colours.");
            // Every voxel's colour is one of the three palette entries, and the two oranges now agree.
            foreach (Rgba32 c in merged.Colours)
            {
                CollectionAssert.Contains(merged.Palette, c);
            }
            Assert.AreEqual(merged.Colours[3], merged.Colours[4], "The two nearest (orange) shades merged.");
        }

        [Test]
        public void Consolidate_MaxColours_NeverInventsColoursBeyondWhatExists()
        {
            var colours = new[] { Red, Blue };

            ColourModes.PaletteAssignment merged = ColourModes.AssignPalette(
                colours, mask: null, ColourMode.Consolidated,
                new ColourModes.Options { ConsolidateTolerance = 0f, ConsolidateMaxColours = 8 });

            Assert.AreEqual(2, merged.Palette!.Length, "A cap above the distinct-colour count yields fewer, not more.");
        }

        private static VoxelGrid FilledGrid(int nx, int ny, int nz)
        {
            var grid = new VoxelGrid(nx, ny, nz) { Origin = g3.Vector3d.Zero, CellSize = 1.0 };
            for (int i = 0; i < grid.Occupied.Length; i++)
            {
                grid.Occupied[i] = true;
            }
            return grid;
        }

        [Test]
        public void Potts_StrengthZero_IsIdentity()
        {
            VoxelGrid grid = FilledGrid(3, 3, 1);
            var palette = new[] { Red, Blue };
            var sampled = new Rgba32[grid.Occupied.Length];
            var labels = new int[grid.Occupied.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                sampled[i] = Red;
                labels[i] = i % 2;
            }

            int[] result = PottsLabelSmoother.Smooth(grid, sampled, labels, palette, strength: 0f);

            Assert.AreNotSame(labels, result);
            CollectionAssert.AreEqual(labels, result);
        }

        [Test]
        public void Potts_FlipsSpeckleLabelInUniformRegion()
        {
            // Every voxel sampled the same red, but the centre carries a speckle mis-label. The
            // smoother must flip it back to the region's label.
            VoxelGrid grid = FilledGrid(3, 3, 1);
            var palette = new[] { Red, new Rgba32(120, 20, 20, 255) };
            var sampled = new Rgba32[grid.Occupied.Length];
            var labels = new int[grid.Occupied.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                sampled[i] = Red;
                labels[i] = 0;
            }
            labels[grid.Index(1, 1, 0)] = 1;

            int[] result = PottsLabelSmoother.Smooth(grid, sampled, labels, palette, strength: 1f);

            Assert.AreEqual(0, result[grid.Index(1, 1, 0)], "Speckle flips to the surrounding label.");
        }

        [Test]
        public void Potts_PinsRealColourBoundary()
        {
            // Two honest regions (left sampled red, right sampled blue) with a strong source edge:
            // even a heavy strength must not move the boundary.
            VoxelGrid grid = FilledGrid(4, 3, 1);
            var palette = new[] { Red, Blue };
            var sampled = new Rgba32[grid.Occupied.Length];
            var labels = new int[grid.Occupied.Length];
            for (int z = 0; z < 1; z++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int i = grid.Index(x, y, z);
                        sampled[i] = x < 2 ? Red : Blue;
                        labels[i] = x < 2 ? 0 : 1;
                    }
                }
            }

            int[] result = PottsLabelSmoother.Smooth(grid, sampled, labels, palette, strength: 2f);

            CollectionAssert.AreEqual(labels, result, "A pinned real edge must survive smoothing.");
        }

        [Test]
        public void Metrics_CountsVoxelsFacesAndColours()
        {
            VoxelGrid fine = FilledGrid(2, 1, 1);
            FineGridAnalysis analysis = FineGridAnalysis.Build(fine, 1);
            GridPlacementSearch.Placement placement = GridPlacementSearch.Materialise(
                analysis, GridPlacementSearch.IdentityCandidate(analysis),
                new GridPlacementSearch.Options { Coverage = 0.5f, FaceWeight = 1f, IouWeight = 1f, GapWeight = 2f });

            var colours = new Rgba32[placement.Grid.Occupied.Length];
            colours[placement.Grid.Index(0, 0, 0)] = Red;
            colours[placement.Grid.Index(1, 0, 0)] = Blue;

            SpikeMetrics metrics = SpikeMetrics.Compute(
                placement.Grid, colours, placement, floatersRemoved: 3, new g3.Vector3i(2, 1, 1));

            Assert.AreEqual(2, metrics.VoxelCount);
            Assert.AreEqual(10, metrics.ExposedFaces, "Two joined cubes expose 10 faces.");
            Assert.AreEqual(2, metrics.DistinctColours);
            Assert.AreEqual(3, metrics.FloatersRemoved);
            Assert.AreEqual(1f, metrics.SIou, 1e-4f);
        }

        [Test]
        public void FullPipeline_CubeObj_RunsEndToEnd()
        {
            string objPath = Path.Combine(Path.GetTempPath(), "spike-pipeline-cube.obj");
            File.WriteAllText(objPath, CubeObj());
            try
            {
                // Every pass switched on ("with"-expressions on structs need C# 10, so spelled out).
                var settings = new Settings
                {
                    ResolutionInput = ResolutionInput.MaxDimSlider,
                    MaxDimVoxels = 8,
                    GridSearch = true,
                    ScaleFlex = true,
                    ThinFeatureKeep = true,
                    FineFactor = 2,
                    Coverage = 0.5f,
                    RemoveFloaters = true,
                    CleanupStrength = 1,
                    FaceWeight = 1f,
                    IouWeight = 1f,
                    GapWeight = 2f,
                    UvDilate = true,
                    UvDilatePasses = UvIslandDilation.DefaultPasses,
                    MultiSampleColour = true,
                    PottsStrength = 0.5f,
                    ColourMode = ColourMode.PerModelPalette,
                    PaletteSize = 4,
                    TaubinPasses = 2,
                    TaubinLambda = 0.5f,
                    TaubinMu = 0.53f,
                };

                SpikeStageResult result = SpikePipeline.Run(objPath, settings);

                Assert.Greater(result.VoxelCount, 0, "Cube must produce voxels.");
                Assert.Greater(result.Blocky.vertexCount, 0, "Blocky mesh must have geometry.");
                Assert.AreEqual(result.VoxelCount, result.Metrics.VoxelCount, "Metrics count the final grid.");
                Assert.Greater(result.Metrics.ExposedFaces, 0);
                Assert.Greater(result.Metrics.Score, 0f);
                Assert.IsTrue(result.Occupancy.IsOccupied(
                    result.Occupancy.NX / 2, result.Occupancy.NY / 2, result.Occupancy.NZ / 2),
                    "Cube centre is inside.");
            }
            finally
            {
                File.Delete(objPath);
            }
        }

        private static string CubeObj() => string.Join("\n",
            "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
            "v 0 0 1", "v 1 0 1", "v 1 1 1", "v 0 1 1",
            "f 1 2 3", "f 1 3 4",
            "f 5 8 7", "f 5 7 6",
            "f 1 5 6", "f 1 6 2",
            "f 2 6 7", "f 2 7 3",
            "f 3 7 8", "f 3 8 4",
            "f 4 8 5", "f 4 5 1",
            "");
    }
}
