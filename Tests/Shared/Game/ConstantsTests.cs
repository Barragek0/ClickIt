namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class ConstantsTests
    {
        [TestMethod]
        public void HeistContractNames_ContainsExpectedEntries()
        {
            Constants.HeistQuestContractNames.Should().Contain("Contract: Trial Run");
            Constants.HeistQuestContractNames.Should().Contain("Contract: The Finest Costumes");
        }
    }
}