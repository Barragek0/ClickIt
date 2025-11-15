using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class MergedUtilsConstantsTests
    {
        [TestMethod]
        public void IndexAndModIndexConstants_ShouldBeSequential0To7()
        {
            // ModIndexConstants
            ModIndexConstants.First.Should().Be(0);
            ModIndexConstants.Second.Should().Be(1);
            ModIndexConstants.Third.Should().Be(2);
            ModIndexConstants.Fourth.Should().Be(3);
            ModIndexConstants.Fifth.Should().Be(4);
            ModIndexConstants.Sixth.Should().Be(5);
            ModIndexConstants.Seventh.Should().Be(6);
            ModIndexConstants.Eighth.Should().Be(7);

            // IndexConstants
            IndexConstants.First.Should().Be(0);
            IndexConstants.Second.Should().Be(1);
            IndexConstants.Third.Should().Be(2);
            IndexConstants.Fourth.Should().Be(3);
            IndexConstants.Fifth.Should().Be(4);
            IndexConstants.Sixth.Should().Be(5);
            IndexConstants.Seventh.Should().Be(6);
            IndexConstants.Eighth.Should().Be(7);
        }

        [TestMethod]
        public void WeightTypeConstants_ShouldContainExpectedKeys()
        {
            WeightTypeConstants.TopDownside.Should().Be("topdownside");
            WeightTypeConstants.BottomDownside.Should().Be("bottomdownside");
            WeightTypeConstants.TopUpside.Should().Be("topupside");
            WeightTypeConstants.BottomUpside.Should().Be("bottomupside");
        }

        [TestMethod]
        public void AltarWeights_Indexer_SetGet_AllKnownTypes()
        {
            var w = new AltarWeights();
            // set values for each known weight type at index 0..3 and verify
            w[WeightTypeConstants.TopDownside, 0] = 1.1m;
            w[WeightTypeConstants.BottomDownside, 1] = 2.2m;
            w[WeightTypeConstants.TopUpside, 2] = 3.3m;
            w[WeightTypeConstants.BottomUpside, 3] = 4.4m;

            w[WeightTypeConstants.TopDownside, 0].Should().Be(1.1m);
            w[WeightTypeConstants.BottomDownside, 1].Should().Be(2.2m);
            w[WeightTypeConstants.TopUpside, 2].Should().Be(3.3m);
            w[WeightTypeConstants.BottomUpside, 3].Should().Be(4.4m);
        }
    }
}
