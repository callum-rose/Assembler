using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class EyeReprojectionTests
    {
        // A 5×5×5 solid box spanning voxel coords 0..4 on each axis.
        private static VoxelModel Box() => VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));

        // The Front view looks along +X, so its rays enter through the x = Min face.
        private static VoxelViewProjection FrontProjection(VoxelModel model) =>
            new(OrthographicView.Front, model);

        private const int ImageSize = 128;

        [Test]
        public void TrySnapPick_LandsOnTheSurfaceVoxelUnderThePick()
        {
            VoxelModel model = Box();
            VoxelViewProjection projection = FrontProjection(model);

            // Aim at the centre of the voxel (0, 3, 3) on the front (x = 0) face.
            Vector2 pick = projection.WorldToNormalized(new Vector3(0.5f, 3.5f, 3.5f));

            bool ok = EyeReprojection.TrySnapPick(
                model, projection, ImageSize, pick, 2f,
                out Vector3Int voxel, out Vector3 normal, out int support);

            Assert.IsTrue(ok);
            Assert.AreEqual(new Vector3Int(0, 3, 3), voxel);
            Assert.AreEqual(new Vector3(-1, 0, 0), normal); // faces the camera on -X
            Assert.Greater(support, 0);
        }

        [Test]
        public void TrySnapPick_RecoversAPickJustOffTheSilhouette()
        {
            VoxelModel model = Box();
            VoxelViewProjection projection = FrontProjection(model);

            // A pick nudged a voxel beyond the top edge would miss with a single ray, but the
            // neighbourhood search should still find the top row of the front face.
            float voxelNorm = projection.VoxelPixelSize(ImageSize) / ImageSize;
            Vector2 edge = projection.WorldToNormalized(new Vector3(0.5f, 2.5f, 4.5f));
            Vector2 offEdge = edge - new Vector2(0f, voxelNorm); // push up, off the silhouette

            bool ok = EyeReprojection.TrySnapPick(
                model, projection, ImageSize, offEdge, 2f,
                out Vector3Int voxel, out _, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, voxel.x);
            Assert.AreEqual(4, voxel.z); // snapped back onto the topmost occupied row
        }

        [Test]
        public void TrySnapPick_ReturnsFalseWhenTheNeighbourhoodMissesEntirely()
        {
            VoxelModel model = Box();
            VoxelViewProjection projection = FrontProjection(model);

            bool ok = EyeReprojection.TrySnapPick(
                model, projection, ImageSize, new Vector2(0.99f, 0.01f), 1f,
                out _, out _, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void SymmetryPlane_ForFrontViewIsTheHorizontalYAxisThroughTheCentre()
        {
            VoxelModel model = Box();

            (Vector3 point, Vector3 normal) = EyeReprojection.SymmetryPlane(model, OrthographicView.Front);

            Assert.AreEqual(new Vector3(0, 1, 0), normal);
            Assert.AreEqual(2.5f, point.y, 1e-4f); // (0..4) → centre plane at 2.5
        }

        [Test]
        public void ReflectPoint_MirrorsAcrossThePlane()
        {
            Vector3 mirrored = EyeReprojection.ReflectPoint(
                new Vector3(0.5f, 3.5f, 3.5f), new Vector3(0, 2.5f, 0), new Vector3(0, 1, 0));

            Assert.AreEqual(new Vector3(0.5f, 1.5f, 3.5f), mirrored);
        }

        [Test]
        public void BuildAnchors_MirrorsASinglePickIntoASymmetricPair()
        {
            VoxelModel model = Box();
            VoxelViewProjection projection = FrontProjection(model);
            var options = new EyePlacementOptions
            {
                View = OrthographicView.Front,
                AutoOrient = false,
                EyeCount = 2,
                AssumeSymmetry = true,
                ImageSize = ImageSize,
                SurfaceOffset = 0f,
            };

            // Only one pick, high on the +Y side of the face.
            var picks = new List<Vector2> { projection.WorldToNormalized(new Vector3(0.5f, 3.5f, 3.5f)) };

            IReadOnlyList<EyeAnchor> anchors = EyeReprojection.BuildAnchors(model, projection, options, picks);

            Assert.AreEqual(2, anchors.Count);
            // The pair is symmetric about y = 2 (voxel rows 3 and 1), same x and z.
            Assert.AreEqual(4f, anchors[0].Position.y + anchors[1].Position.y, 1e-3f);
            Assert.AreEqual(anchors[0].Position.x, anchors[1].Position.x, 1e-3f);
            Assert.AreEqual(anchors[0].Position.z, anchors[1].Position.z, 1e-3f);
        }

        [Test]
        public void BuildAnchors_WithoutSymmetrySnapsEachPickIndependently()
        {
            VoxelModel model = Box();
            VoxelViewProjection projection = FrontProjection(model);
            var options = new EyePlacementOptions
            {
                View = OrthographicView.Front,
                AutoOrient = false,
                EyeCount = 2,
                AssumeSymmetry = false,
                ImageSize = ImageSize,
                SurfaceOffset = 0f,
            };

            var picks = new List<Vector2>
            {
                projection.WorldToNormalized(new Vector3(0.5f, 1.5f, 3.5f)),
                projection.WorldToNormalized(new Vector3(0.5f, 3.5f, 3.5f)),
            };

            IReadOnlyList<EyeAnchor> anchors = EyeReprojection.BuildAnchors(model, projection, options, picks);

            Assert.AreEqual(2, anchors.Count);
        }
    }
}
