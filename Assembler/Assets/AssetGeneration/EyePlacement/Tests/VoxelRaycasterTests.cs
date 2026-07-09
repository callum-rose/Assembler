using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class VoxelRaycasterTests
    {
        [Test]
        public void HitsFrontFaceWithFacingNormal()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(3, 3, 3));

            bool hit = VoxelRaycaster.TryRaycast(
                model, new Vector3(1.5f, 1.5f, -5f), new Vector3(0, 0, 1), 20f,
                out Vector3Int cell, out Vector3 normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3Int(1, 1, 0), cell);
            Assert.AreEqual(new Vector3(0, 0, -1), normal);
        }

        [Test]
        public void HitsSideFaceWhenApproachedFromTheSide()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(3, 3, 3));

            bool hit = VoxelRaycaster.TryRaycast(
                model, new Vector3(-5f, 1.5f, 1.5f), new Vector3(1, 0, 0), 20f,
                out Vector3Int cell, out Vector3 normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3Int(0, 1, 1), cell);
            Assert.AreEqual(new Vector3(-1, 0, 0), normal);
        }

        [Test]
        public void MissesWhenTheRayClearsTheModel()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(3, 3, 3));

            bool hit = VoxelRaycaster.TryRaycast(
                model, new Vector3(10f, 10f, -5f), new Vector3(0, 0, 1), 20f,
                out _, out _);

            Assert.IsFalse(hit);
        }
    }
}
