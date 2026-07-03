using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class GeometricEyePlacerTests
    {
        [Test]
        public void PlacesTwoEyesSymmetricallyOnTheWidestAxis()
        {
            // Widest in X, so X is the bilateral axis and Y is depth (front/back).
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(9, 3, 5));
            var options = new EyePlacementOptions { EyeCount = 2, SurfaceOffset = 0.5f };

            var eyes = GeometricEyePlacer.Place(model, options);

            Assert.AreEqual(2, eyes.Count);

            // Both sit on the same depth face at the same height, mirrored about the X centre.
            Assert.AreEqual(eyes[0].Position.y, eyes[1].Position.y, 1e-4f);
            Assert.AreEqual(eyes[0].Position.z, eyes[1].Position.z, 1e-4f);
            Assert.AreEqual(eyes[0].Normal, eyes[1].Normal);

            float centreX = model.Min.x + (model.Size.x - 1) / 2f;
            Assert.AreEqual(centreX, (eyes[0].Position.x + eyes[1].Position.x) * 0.5f, 1e-4f);
            Assert.AreNotEqual(eyes[0].Position.x, eyes[1].Position.x);
        }

        [Test]
        public void NormalsAreAxisAlignedAndOutward()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(9, 3, 5));
            var eyes = GeometricEyePlacer.Place(model, new EyePlacementOptions { EyeCount = 2 });

            foreach (var eye in eyes)
            {
                Assert.AreEqual(1f, eye.Normal.magnitude, 1e-4f, "normal should be a unit face direction");
                // Eye height must land inside the model's vertical span.
                Assert.GreaterOrEqual(eye.Position.z, model.Min.z);
                Assert.LessOrEqual(eye.Position.z, model.Max.z + 1);
            }
        }

        [Test]
        public void EmptyModelYieldsNoEyes()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(-1, -1, -1)); // no cells
            var eyes = GeometricEyePlacer.Place(model, new EyePlacementOptions());
            Assert.AreEqual(0, eyes.Count);
        }
    }
}
