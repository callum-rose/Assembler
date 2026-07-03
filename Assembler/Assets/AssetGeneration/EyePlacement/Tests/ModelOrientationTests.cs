using NUnit.Framework;
using UnityEngine;
using Assembler.AssetGeneration.ImageOrientation;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class ModelOrientationTests
    {
        [Test]
        public void FrontYawMapsCompassCodesToGroundPlaneAngles()
        {
            Assert.AreEqual(0f, ModelOrientation.FrontYawDegrees(FacingDirection.Right));
            Assert.AreEqual(90f, ModelOrientation.FrontYawDegrees(FacingDirection.Up));
            Assert.AreEqual(180f, ModelOrientation.FrontYawDegrees(FacingDirection.Left));
            Assert.AreEqual(270f, ModelOrientation.FrontYawDegrees(FacingDirection.Down));
            Assert.AreEqual(45f, ModelOrientation.FrontYawDegrees(FacingDirection.RightUp));
            Assert.AreEqual(315f, ModelOrientation.FrontYawDegrees(FacingDirection.RightDown));
        }

        [Test]
        public void ViewFacingFrontLooksAtTheFrontFromInFront()
        {
            // Front points +X (Right). The camera should stand on the +X side (yaw+180) with the
            // three-quarter offset, i.e. FromZUpAngles(0 + 180 + 45, pitch).
            var view = ModelOrientation.ViewFacingFront(FacingDirection.Right, 30f, 45f);
            var expected = OrthographicView.FromZUpAngles(225f, 30f);

            Assert.AreEqual(expected.Forward.x, view.Forward.x, 1e-4f);
            Assert.AreEqual(expected.Forward.y, view.Forward.y, 1e-4f);
            Assert.AreEqual(expected.Forward.z, view.Forward.z, 1e-4f);

            // Looking at the +X front means the view direction has a negative X component.
            Assert.Less(view.Forward.x, 0f);
        }

        [Test]
        public void UnknownFrontFallsBackToIsometric()
        {
            var view = ModelOrientation.ViewFacingFront(null, 30f, 45f);
            var iso = OrthographicView.Isometric;
            Assert.AreEqual(iso.Forward.x, view.Forward.x, 1e-4f);
            Assert.AreEqual(iso.Forward.y, view.Forward.y, 1e-4f);
            Assert.AreEqual(iso.Forward.z, view.Forward.z, 1e-4f);
        }
    }
}
