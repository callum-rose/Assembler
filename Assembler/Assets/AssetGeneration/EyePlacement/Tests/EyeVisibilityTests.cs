using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class EyeVisibilityTests
    {
        [Test]
        public void EyeOnAFaceIsVisibleFromThatSide()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(3, 3, 3));
            var eye = new EyeAnchor(new Vector3(3.5f, 1f, 1f), new Vector3(1, 0, 0)); // on the +X face
            var projection = new VoxelViewProjection(OrthographicView.FromZUpAngles(180f, 0f), model); // looks at +X

            Assert.IsFalse(EyeVisibility.IsMasked(model, eye, projection));
        }

        [Test]
        public void EyeOnTheFarSideIsMasked()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(3, 3, 3));
            var eye = new EyeAnchor(new Vector3(3.5f, 1f, 1f), new Vector3(1, 0, 0)); // on the +X face
            var projection = new VoxelViewProjection(OrthographicView.FromZUpAngles(0f, 0f), model); // camera on the -X side

            Assert.IsTrue(EyeVisibility.IsMasked(model, eye, projection));
        }

        [Test]
        public void EmptyModelIsNeverMasked()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(-1, -1, -1));
            var eye = new EyeAnchor(Vector3.zero, Vector3.up);
            var projection = new VoxelViewProjection(OrthographicView.Isometric, model);

            Assert.IsFalse(EyeVisibility.IsMasked(model, eye, projection));
        }
    }
}
