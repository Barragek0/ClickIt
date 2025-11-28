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
            global::ClickIt.Constants.Constants.CleansingFireAltar.Should().Be("CleansingFireAltar");
            global::ClickIt.Constants.Constants.MouseEventLeftDown.Should().Be(0x02);
            global::ClickIt.Constants.Constants.KeyPressed.Should().Be(0x8000);
            global::ClickIt.Constants.Constants.PipeSeparator.Should().Be("|");
            global::ClickIt.Constants.Constants.MinModWeight.Should().BeGreaterThan(0);
            global::ClickIt.Constants.Constants.MaxModWeight.Should().BeGreaterOrEqualTo(global::ClickIt.Constants.Constants.MinModWeight);
        }

        [TestMethod]
        public void HeistContractNames_ContainsExpectedEntries()
        {
            global::ClickIt.Constants.Constants.HeistQuestContractNames.Should().Contain("Contract: Trial Run");
            global::ClickIt.Constants.Constants.HeistQuestContractNames.Should().Contain("Contract: The Finest Costumes");
        }
    }
}

