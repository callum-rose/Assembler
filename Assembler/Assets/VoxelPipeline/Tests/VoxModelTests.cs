using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Assembler.AssetGeneration.VoxelPipeline;

namespace Tests.AssetGeneration.VoxelPipeline
{
    public sealed class VoxModelTests
    {
        [Test]
        public void Index_IsContiguousAndUnique()
        {
            var model = new VoxModel(3, 4, 5);
            var seen = new HashSet<int>();
            for (int z = 0; z < model.Z; z++)
            {
                for (int y = 0; y < model.Y; y++)
                {
                    for (int x = 0; x < model.X; x++)
                    {
                        int i = model.Index(x, y, z);
                        Assert.IsTrue(i >= 0 && i < model.Occupied.Length, $"index {i} out of range");
                        Assert.IsTrue(seen.Add(i), $"duplicate index for ({x},{y},{z})");
                    }
                }
            }
            Assert.AreEqual(3 * 4 * 5, seen.Count);
        }

        [Test]
        public void InBounds_RejectsOutsideGrid()
        {
            var model = new VoxModel(2, 2, 2);
            Assert.IsTrue(model.InBounds(0, 0, 0));
            Assert.IsTrue(model.InBounds(1, 1, 1));
            Assert.IsFalse(model.InBounds(-1, 0, 0));
            Assert.IsFalse(model.InBounds(0, 2, 0));
            Assert.IsFalse(model.InBounds(0, 0, 2));
        }

        [Test]
        public void FromResult_PreservesDimsAndOccupancy()
        {
            var cells = new List<VoxCell>
            {
                new VoxCell(0, 0, 0, new Color32(255, 0, 0, 255)),
                new VoxCell(1, 2, 3, new Color32(0, 255, 0, 255)),
            };
            var result = new VoxResult(2, 3, 4, cells);

            VoxModel model = VoxModel.FromResult(result);

            Assert.AreEqual(2, model.X);
            Assert.AreEqual(3, model.Y);
            Assert.AreEqual(4, model.Z);
            Assert.IsTrue(model.IsOccupied(0, 0, 0));
            Assert.IsTrue(model.IsOccupied(1, 2, 3));
            Assert.IsFalse(model.IsOccupied(1, 1, 1));
        }

        [Test]
        public void RoundTrip_PreservesCellSetAndColors()
        {
            var cells = new List<VoxCell>
            {
                new VoxCell(0, 0, 0, new Color32(10, 20, 30, 255)),
                new VoxCell(2, 1, 0, new Color32(40, 50, 60, 255)),
                new VoxCell(1, 1, 1, new Color32(70, 80, 90, 255)),
            };
            var source = new VoxResult(3, 2, 2, cells);

            VoxResult round = VoxModel.FromResult(source).ToResult();

            Assert.AreEqual(source.GridX, round.GridX);
            Assert.AreEqual(source.GridY, round.GridY);
            Assert.AreEqual(source.GridZ, round.GridZ);
            Assert.AreEqual(cells.Count, round.Cells.Count);

            // Compare as sets — ToResult re-orders into a deterministic scan, which is fine.
            var expected = cells.Select(Key).OrderBy(s => s).ToArray();
            var actual = round.Cells.Select(Key).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void ToResult_EmitsInDeterministicZyxScanOrder()
        {
            var cells = new List<VoxCell>
            {
                new VoxCell(1, 1, 1, new Color32(1, 1, 1, 255)),
                new VoxCell(0, 0, 0, new Color32(2, 2, 2, 255)),
                new VoxCell(1, 0, 0, new Color32(3, 3, 3, 255)),
            };
            VoxResult round = VoxModel.FromResult(new VoxResult(2, 2, 2, cells)).ToResult();

            // Z→Y→X scan: (0,0,0) then (1,0,0) then (1,1,1).
            Assert.AreEqual((0, 0, 0), (round.Cells[0].X, round.Cells[0].Y, round.Cells[0].Z));
            Assert.AreEqual((1, 0, 0), (round.Cells[1].X, round.Cells[1].Y, round.Cells[1].Z));
            Assert.AreEqual((1, 1, 1), (round.Cells[2].X, round.Cells[2].Y, round.Cells[2].Z));
        }

        [Test]
        public void EmptyResult_RoundTripsToEmpty()
        {
            VoxResult round = VoxModel.FromResult(new VoxResult(4, 4, 4, new List<VoxCell>())).ToResult();
            Assert.AreEqual(0, round.Cells.Count);
            Assert.AreEqual(4, round.GridX);
        }

        private static string Key(VoxCell c) =>
            $"{c.X},{c.Y},{c.Z}:{c.Color.r},{c.Color.g},{c.Color.b},{c.Color.a}";
    }
}
