using NUnit.Framework;
using Assembler.AssetGeneration.ImageOrientation;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    /// <summary>Covers the ImageOrientation discriminated union + its index parsing (no test asmdef of its own).</summary>
    public sealed class OrientationAnswerTests
    {
        [Test]
        public void FacingResultExposesDirectionNotIndex()
        {
            var result = new OrientationResult(new OrientationAnswer.Facing(FacingDirection.LeftDown), "LD");

            Assert.AreEqual(FacingDirection.LeftDown, result.Direction);
            Assert.IsNull(result.Index);
            Assert.AreEqual("LD", result.Code);
        }

        [Test]
        public void ViewIndexResultExposesIndexNotDirection()
        {
            var result = new OrientationResult(new OrientationAnswer.ViewIndex(3), "3");

            Assert.AreEqual(3, result.Index);
            Assert.IsNull(result.Direction);
            Assert.AreEqual("#3", result.Code);
        }

        [Test]
        public void UnrecognisedResultExposesNeither()
        {
            var result = new OrientationResult(new OrientationAnswer.Unrecognised(), "banana");

            Assert.IsNull(result.Direction);
            Assert.IsNull(result.Index);
            Assert.AreEqual("(unrecognised)", result.Code);
        }

        [Test]
        public void ParseIndexReadsAnIntegerAnywhereInTheReply()
        {
            Assert.AreEqual(2, ImageFacingDirection.ParseIndex("2"));
            Assert.AreEqual(5, ImageFacingDirection.ParseIndex("The best view is 5."));
            Assert.IsNull(ImageFacingDirection.ParseIndex("none of them"));
            Assert.IsNull(ImageFacingDirection.ParseIndex(""));
        }
    }
}
