namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class ConstantsTests
    {
        [TestMethod]
        public void BasicConstants_AreSet()
        {
            Constants.CleansingFireAltar.Should().Be("CleansingFireAltar");
            Constants.MouseEventLeftDown.Should().Be(0x02);
            Constants.KeyPressed.Should().Be(0x8000);
            Constants.PipeSeparator.Should().Be("|");
            Constants.MinModWeight.Should().BeGreaterThan(0);
            Constants.MaxModWeight.Should().BeGreaterOrEqualTo(Constants.MinModWeight);
        }

        [TestMethod]
        public void HeistContractNames_ContainsExpectedEntries()
        {
            Constants.HeistQuestContractNames.Should().Contain("Contract: Trial Run");
            Constants.HeistQuestContractNames.Should().Contain("Contract: The Finest Costumes");
        }
    }
}