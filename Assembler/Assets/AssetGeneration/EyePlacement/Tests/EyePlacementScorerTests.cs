using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class EyePlacementScorerTests
    {
        // A 5×5×5 solid box spanning voxel coords 0..4 on each axis. The x = 0 face is a valid
        // side for eyes; the z = 4 face is the top (an "up" surface eyes should never sit on).
        private static VoxelModel Box() => VoxelBox.Solid(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));

        private static readonly EyeScoreOptions Options = new();

        // Mirrors EyeReprojection: an anchor sits at the voxel index pushed out along its normal.
        private static EyeAnchor Anchor(Vector3Int voxel, Vector3 normal, float offset = 0.5f) =>
            new((Vector3)voxel + normal * offset, normal);

        private static EyeGroundTruth Truth(params GroundTruthEye[] eyes) =>
            new("box", eyes);

        [Test]
        public void CorrectlySeatedPair_Passes()
        {
            VoxelModel model = Box();
            EyeGroundTruth truth = Truth(
                new GroundTruthEye(new Vector3(0, 1, 3)),
                new GroundTruthEye(new Vector3(0, 3, 3)));
            var anchors = new List<EyeAnchor>
            {
                Anchor(new Vector3Int(0, 1, 3), new Vector3(-1, 0, 0)),
                Anchor(new Vector3Int(0, 3, 3), new Vector3(-1, 0, 0)),
            };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            Assert.IsTrue(score.Pass, score.Summary);
            Assert.AreEqual(0, score.ExtraAnchors);
            CollectionAssert.AreEqual(new[] { true, true }, ForEachPass(score));
        }

        [Test]
        public void UpFacingNormal_FailsEvenWhenOnTargetAndOnSurface()
        {
            VoxelModel model = Box();
            EyeGroundTruth truth = Truth(new GroundTruthEye(new Vector3(2, 2, 4)));
            var anchors = new List<EyeAnchor> { Anchor(new Vector3Int(2, 2, 4), new Vector3(0, 0, 1)) };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            EyeScore eye = score.Eyes[0];
            Assert.IsTrue(eye.WithinTolerance, "should be on target");
            Assert.IsTrue(eye.OnSurface, "the top voxel is a surface voxel");
            Assert.IsFalse(eye.NormalNotUp, "an up (+Z) normal must fail");
            Assert.IsFalse(score.Pass);
            StringAssert.Contains("up", eye.Reason);
        }

        [Test]
        public void OffModelAnchor_FailsTheSurfaceCheck()
        {
            VoxelModel model = Box();
            // Generous radius so it clears the distance check — isolating the surface test.
            EyeGroundTruth truth = Truth(new GroundTruthEye(new Vector3(0, 3, 3), RadiusVoxels: 6f));
            var anchors = new List<EyeAnchor> { new(new Vector3(-3f, 3.5f, 3.5f), new Vector3(-1, 0, 0)) };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            EyeScore eye = score.Eyes[0];
            Assert.IsTrue(eye.WithinTolerance);
            Assert.IsFalse(eye.OnSurface, "an anchor floating off the model must fail");
            Assert.IsFalse(score.Pass);
        }

        [Test]
        public void FarAnchor_FailsTheDistanceCheck()
        {
            VoxelModel model = Box();
            EyeGroundTruth truth = Truth(new GroundTruthEye(new Vector3(0, 0, 0)));
            // On the surface, but at the opposite corner from the ground-truth eye.
            var anchors = new List<EyeAnchor> { Anchor(new Vector3Int(0, 4, 4), new Vector3(-1, 0, 0)) };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            Assert.IsFalse(score.Eyes[0].WithinTolerance);
            Assert.IsFalse(score.Pass);
        }

        [Test]
        public void MissingAnchor_FailsThatEye()
        {
            VoxelModel model = Box();
            EyeGroundTruth truth = Truth(
                new GroundTruthEye(new Vector3(0, 1, 3)),
                new GroundTruthEye(new Vector3(0, 3, 3)));
            var anchors = new List<EyeAnchor> { Anchor(new Vector3Int(0, 1, 3), new Vector3(-1, 0, 0)) };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            Assert.IsTrue(score.Eyes[0].Pass);
            Assert.IsNull(score.Eyes[1].Anchor);
            Assert.IsFalse(score.Eyes[1].Pass);
            Assert.IsFalse(score.Pass);
        }

        [Test]
        public void EachGroundTruthEye_ClaimsADistinctAnchor()
        {
            VoxelModel model = Box();
            EyeGroundTruth truth = Truth(
                new GroundTruthEye(new Vector3(0, 1, 3)),
                new GroundTruthEye(new Vector3(0, 3, 3)));
            var anchors = new List<EyeAnchor>
            {
                Anchor(new Vector3Int(0, 1, 3), new Vector3(-1, 0, 0)),
                Anchor(new Vector3Int(0, 3, 3), new Vector3(-1, 0, 0)),
            };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            Assert.AreNotSame(score.Eyes[0].Anchor, score.Eyes[1].Anchor);
            Assert.AreEqual(0, score.ExtraAnchors);
        }

        [Test]
        public void ExtraAnchorBeyondGroundTruth_IsCounted()
        {
            VoxelModel model = Box();
            EyeGroundTruth truth = Truth(new GroundTruthEye(new Vector3(0, 2, 3)));
            var anchors = new List<EyeAnchor>
            {
                Anchor(new Vector3Int(0, 2, 3), new Vector3(-1, 0, 0)),
                Anchor(new Vector3Int(0, 4, 3), new Vector3(-1, 0, 0)),
            };

            ModelScore score = EyePlacementScorer.Score(model, truth, anchors, Options);

            Assert.IsTrue(score.Pass);
            Assert.AreEqual(1, score.ExtraAnchors);
        }

        private static bool[] ForEachPass(ModelScore score)
        {
            var passes = new bool[score.Eyes.Count];
            for (int i = 0; i < score.Eyes.Count; i++)
            {
                passes[i] = score.Eyes[i].Pass;
            }

            return passes;
        }
    }
}
