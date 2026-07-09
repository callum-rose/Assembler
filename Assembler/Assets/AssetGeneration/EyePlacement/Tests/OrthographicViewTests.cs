using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class OrthographicViewTests
    {
        [Test]
        public void IsometricBasisIsOrthonormal()
        {
            var view = OrthographicView.Isometric;

            Assert.AreEqual(1f, view.Right.magnitude, 1e-4f);
            Assert.AreEqual(1f, view.Up.magnitude, 1e-4f);
            Assert.AreEqual(1f, view.Forward.magnitude, 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(view.Right, view.Up), 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(view.Right, view.Forward), 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(view.Up, view.Forward), 1e-4f);
        }

        [Test]
        public void ProjectionRoundTripsThroughTheImagePlane()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(7, 5, 9));
            var projection = new VoxelViewProjection(OrthographicView.Isometric, model);

            var point = new Vector3(3f, 2f, 6f);
            Vector2 normalized = projection.WorldToNormalized(point);
            projection.NormalizedToRay(normalized, out Vector3 origin, out Vector3 direction);

            // The ray origin differs from the point only along the view direction, so it must
            // reproject to the same image coordinate, and the ray must head into the model.
            Vector2 originNormalized = projection.WorldToNormalized(origin);
            Assert.AreEqual(normalized.x, originNormalized.x, 1e-3f);
            Assert.AreEqual(normalized.y, originNormalized.y, 1e-3f);
            Assert.AreEqual(1f, Vector3.Dot(direction, OrthographicView.Isometric.Forward), 1e-4f);
        }

        [Test]
        public void ModelCentreProjectsNearImageCentre()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            var projection = new VoxelViewProjection(OrthographicView.Front, model);

            Vector3 centre = (Vector3)model.Min + (Vector3)model.Size * 0.5f;
            Vector2 normalized = projection.WorldToNormalized(centre);

            Assert.AreEqual(0.5f, normalized.x, 0.05f);
            Assert.AreEqual(0.5f, normalized.y, 0.05f);
        }
    }
}
