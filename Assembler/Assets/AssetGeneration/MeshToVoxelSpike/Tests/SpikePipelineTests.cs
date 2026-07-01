using Assembler.AssetGeneration.MeshToVoxels;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Tests
{
    /// <summary>
    /// Light sanity checks for the spike pipeline — a spike, not a full suite. Voxelises a unit cube
    /// straight from a g3 primitive (no file IO / editor UI needed) and asserts the SDF produces an
    /// inside occupancy grid, a non-empty isosurface, and per-voxel reprojected colour; plus a couple
    /// of pure-logic checks on the colour modes.
    /// </summary>
    public sealed class SpikePipelineTests
    {
        [Test]
        public void UnitCube_YieldsInsideOccupancyNonEmptyIsoAndReprojectedColour()
        {
            g3.DMesh3 cube = UnitCube();

            SdfIsosurface.Result result = SdfIsosurface.Build(cube, 8);

            Assert.Greater(result.Occupancy.OccupiedCount, 0, "SDF should mark inside cells occupied.");
            Assert.Greater(result.Iso.TriangleCount, 0, "Marching cubes should produce a non-empty isosurface.");

            VoxelGrid occ = result.Occupancy;
            Assert.IsTrue(occ.IsOccupied(occ.NX / 2, occ.NY / 2, occ.NZ / 2), "Cube centre should be inside.");

            var red = new Color32(220, 40, 40, 255);
            var model = new ObjToVoxConverter.LoadedModel(
                cube, hasUVs: false, new ObjToVoxConverter.ColorSource { FlatColor = red });
            var tree = new g3.DMeshAABBTree3(cube);
            tree.Build();

            Color32[] colours = ColourReprojector.SampleVoxels(occ, model, tree, normalConsistency: false, result.Field);

            int centre = occ.Index(occ.NX / 2, occ.NY / 2, occ.NZ / 2);
            Assert.AreEqual(red.r, colours[centre].r);
            Assert.AreEqual(red.g, colours[centre].g);
            Assert.AreEqual(red.b, colours[centre].b);
        }

        [Test]
        public void ColourModes_Raw_ReturnsUnchangedCopy()
        {
            var colours = new[] { new Color32(10, 20, 30, 255), new Color32(200, 100, 50, 255) };

            Color32[] result = ColourModes.Apply(colours, mask: null, ColourMode.Raw, default);

            Assert.AreNotSame(colours, result);
            Assert.AreEqual(colours[0].r, result[0].r);
            Assert.AreEqual(colours[1].b, result[1].b);
        }

        [Test]
        public void ColourModes_PerModelPalette_ReducesToRequestedColourCount()
        {
            var colours = new[]
            {
                new Color32(240, 20, 20, 255), new Color32(235, 30, 25, 255), // reds
                new Color32(20, 20, 240, 255), new Color32(25, 30, 235, 255), // blues
            };

            Color32[] result = ColourModes.Apply(
                colours, mask: null, ColourMode.PerModelPalette, new ColourModes.Options { PaletteSize = 2 });

            var distinct = new System.Collections.Generic.HashSet<int>();
            foreach (Color32 c in result)
            {
                distinct.Add((c.r << 16) | (c.g << 8) | c.b);
            }
            Assert.LessOrEqual(distinct.Count, 2, "Two well-separated clusters should collapse to at most two colours.");
        }

        private static g3.DMesh3 UnitCube()
        {
            var generator = new g3.TrivialBox3Generator { Box = g3.Box3d.UnitZeroCentered };
            generator.Generate();
            return generator.MakeDMesh();
        }
    }
}
