using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ConstantsTests
    {
        [TestMethod]
        public void BasicConstants_AreSet()
        {
            Constants.Constants.CleansingFireAltar.Should().Be("CleansingFireAltar");
            Constants.Constants.MouseEventLeftDown.Should().Be(0x02);
            Constants.Constants.KeyPressed.Should().Be(0x8000);
            Constants.Constants.PipeSeparator.Should().Be("|");
            Constants.Constants.MinModWeight.Should().BeGreaterThan(0);
            Constants.Constants.MaxModWeight.Should().BeGreaterOrEqualTo(Constants.Constants.MinModWeight);
        }

        [TestMethod]
        public void HeistContractNames_ContainsExpectedEntries()
        {
            Constants.Constants.HeistQuestContractNames.Should().Contain("Contract: Trial Run");
            Constants.Constants.HeistQuestContractNames.Should().Contain("Contract: The Finest Costumes");
        }
    }
}

