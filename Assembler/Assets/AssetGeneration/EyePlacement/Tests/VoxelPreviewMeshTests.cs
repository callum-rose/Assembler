using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class VoxelPreviewMeshTests
    {
        [Test]
        public void SingleVoxelHasSixQuads()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0));
            var mesh = VoxelPreviewMesh.Build(model);

            Assert.AreEqual(24, mesh.vertexCount); // 6 faces × 4 corners
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void SharedFaceBetweenNeighboursIsCulled()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0));
            var mesh = VoxelPreviewMesh.Build(model);

            // Two cubes share one internal face, culled on both sides: 12 - 2 = 10 quads.
            Assert.AreEqual(40, mesh.vertexCount);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void EmptyModelHasNoGeometry()
        {
            var model = VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(-1, -1, -1));
            var mesh = VoxelPreviewMesh.Build(model);

            Assert.AreEqual(0, mesh.vertexCount);
            Object.DestroyImmediate(mesh);
        }
    }
}
