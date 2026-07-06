using NUnit.Framework;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    public sealed class ModelOrientationTests
    {
        [Test]
        public void CandidateYawsAreEvenlySpreadAroundTheCircle()
        {
            var yaws = ModelOrientation.CandidateYaws(8);

            Assert.AreEqual(8, yaws.Count);
            Assert.AreEqual(0f, yaws[0], 1e-4f);
            Assert.AreEqual(45f, yaws[1], 1e-4f);
            Assert.AreEqual(315f, yaws[7], 1e-4f);
        }

        [Test]
        public void CandidateYawsClampToASaneRange()
        {
            Assert.AreEqual(1, ModelOrientation.CandidateYaws(0).Count);
            Assert.AreEqual(16, ModelOrientation.CandidateYaws(999).Count);
        }

        [Test]
        public void ResolveIndexKeepsValidPicksAndFallsBackToZero()
        {
            Assert.AreEqual(3, ModelOrientation.ResolveIndex(3, 8));
            Assert.AreEqual(0, ModelOrientation.ResolveIndex(null, 8)); // unreadable
            Assert.AreEqual(0, ModelOrientation.ResolveIndex(8, 8));    // out of range (high)
            Assert.AreEqual(0, ModelOrientation.ResolveIndex(-1, 8));   // out of range (low)
        }
    }
}
