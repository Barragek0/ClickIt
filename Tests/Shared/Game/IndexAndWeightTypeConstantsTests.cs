namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class ConstantsUtilsTests
    {
        [TestMethod]
        public void IndexConstants_ValuesAreSequential()
        {
            IndexConstants.First.Should().Be(0);
            IndexConstants.Second.Should().Be(1);
            IndexConstants.Eighth.Should().Be(7);
        }

        [TestMethod]
        public void ModIndexConstants_ValuesAreSequential()
        {
            ModIndexConstants.First.Should().Be(0);
            ModIndexConstants.Fourth.Should().Be(3);
            ModIndexConstants.Eighth.Should().Be(7);
        }

        [TestMethod]
        public void WeightTypeConstants_ContainsExpectedKeys()
        {
            WeightTypeConstants.TopDownside.Should().Be("topdownside");
            WeightTypeConstants.TopUpside.Should().Be("topupside");
        }
    }
}
