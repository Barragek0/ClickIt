using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Linq;

namespace ClickIt.Tests
{
    [TestClass]
    public class CartographerChiselModTests
    {
        [TestMethod]
        public void CartographerChiselMod_ShouldExistInUpsideMods()
        {
            // Act
            var cartographerMod = AltarModsConstants.UpsideMods
                .FirstOrDefault(m => m.Id.Contains("Cartographer") && m.Id.Contains("Chisels") && m.Type == "Boss");

            // Assert
            cartographerMod.Should().NotBeNull("Boss Cartographer's Chisels mod should exist");
            cartographerMod.Id.Should().Be("Final Boss drops # additional Cartographer's Chisels");
            cartographerMod.Type.Should().Be("Boss");
            cartographerMod.DefaultValue.Should().Be(8);
        }

        [TestMethod]
        public void CartographerChiselMod_CleanedStringComparison()
        {
            // Arrange
            string inputMod = "FinalBossdrops3additionalCartographer'sChisels";
            string constantMod = "Final Boss drops # additional Cartographer's Chisels";

            // Act - simulate the cleaning process used in TryMatchMod
            string cleanedInput = new string(inputMod.Where(char.IsLetter).ToArray());
            string cleanedConstant = new string(constantMod.Where(char.IsLetter).ToArray());

            // Assert
            cleanedInput.Should().Be("FinalBossdropsadditionalCartographersChisels");
            cleanedConstant.Should().Be("FinalBossdropsadditionalCartographersChisels");
            cleanedInput.Should().BeEquivalentTo(cleanedConstant, "cleaned strings should match");
        }

        [TestMethod]
        public void CartographerChiselMod_CaseInsensitiveComparison()
        {
            // Arrange
            string inputMod = "FinalBossdrops3additionalCartographer'sChisels";
            string constantMod = "Final Boss drops # additional Cartographer's Chisels";

            // Act
            string cleanedInput = new string(inputMod.Where(char.IsLetter).ToArray());
            string cleanedConstant = new string(constantMod.Where(char.IsLetter).ToArray());

            // Assert
            cleanedInput.Equals(cleanedConstant, System.StringComparison.OrdinalIgnoreCase).Should().BeTrue("should match case-insensitively");
        }
    }
}