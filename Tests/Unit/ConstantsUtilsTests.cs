using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ConstantsUtilsTests
    {
        [TestMethod]
        public void IndexConstants_ValuesAreSequential()
        {
            global::ClickIt.Utils.IndexConstants.First.Should().Be(0);
            global::ClickIt.Utils.IndexConstants.Second.Should().Be(1);
            global::ClickIt.Utils.IndexConstants.Eighth.Should().Be(7);
        }

        [TestMethod]
        public void ModIndexConstants_ValuesAreSequential()
        {
            global::ClickIt.Utils.ModIndexConstants.First.Should().Be(0);
            global::ClickIt.Utils.ModIndexConstants.Fourth.Should().Be(3);
            global::ClickIt.Utils.ModIndexConstants.Eighth.Should().Be(7);
        }

        [TestMethod]
        public void WeightTypeConstants_ContainsExpectedKeys()
        {
            global::ClickIt.Utils.WeightTypeConstants.TopDownside.Should().Be("topdownside");
            global::ClickIt.Utils.WeightTypeConstants.TopUpside.Should().Be("topupside");
        }
    }
}
