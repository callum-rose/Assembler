using System;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class EyeGroundTruthTests
    {
        [Test]
        public void ParsesAHandAuthoredSidecar()
        {
            const string json = @"{
              ""name"": ""spotted_cow"",
              ""note"": ""from the 8-yaw ring"",
              ""eyes"": [
                { ""center"": { ""x"": 12, ""y"": 5,  ""z"": 18 }, ""radiusVoxels"": 2 },
                { ""center"": { ""x"": 12, ""y"": 15, ""z"": 18 } }
              ]
            }";

            EyeGroundTruth truth = EyeGroundTruth.FromJson(json);

            Assert.AreEqual("spotted_cow", truth.Name);
            Assert.AreEqual(2, truth.Eyes.Count);
            Assert.AreEqual(new Vector3(12, 5, 18), truth.Eyes[0].Center);
            Assert.AreEqual(2f, truth.Eyes[0].RadiusVoxels);
            Assert.AreEqual(new Vector3(12, 15, 18), truth.Eyes[1].Center);
            Assert.AreEqual(0f, truth.Eyes[1].RadiusVoxels, "omitted radius defers to the scorer default");
        }

        [Test]
        public void RoundTripsThroughJson()
        {
            var original = new EyeGroundTruth("turtle", new[]
            {
                new GroundTruthEye(new Vector3(3, 2, 9), 1.5f),
                new GroundTruthEye(new Vector3(3, 8, 9), 1.5f),
            }, "note");

            EyeGroundTruth reparsed = EyeGroundTruth.FromJson(original.ToJson());

            Assert.AreEqual(original.Name, reparsed.Name);
            Assert.AreEqual(original.Note, reparsed.Note);
            Assert.AreEqual(original.Eyes.Count, reparsed.Eyes.Count);
            for (int i = 0; i < original.Eyes.Count; i++)
            {
                Assert.AreEqual(original.Eyes[i].Center, reparsed.Eyes[i].Center);
                Assert.AreEqual(original.Eyes[i].RadiusVoxels, reparsed.Eyes[i].RadiusVoxels);
            }
        }

        [Test]
        public void RejectsEmptyJson()
        {
            Assert.Throws<ArgumentException>(() => EyeGroundTruth.FromJson(" "));
        }

        [Test]
        public void RejectsSidecarWithNoEyes()
        {
            Assert.Throws<ArgumentException>(() => EyeGroundTruth.FromJson(@"{ ""name"": ""x"", ""eyes"": [] }"));
        }
    }
}
