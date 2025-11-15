using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Linq;
using System;

namespace ClickIt.Tests.Constants
{
    [TestClass]
    public class MergedConstantsTests
    {
        [TestMethod]
        public void CoreStringConstants_ShouldBePresentAndDistinct()
        {
            // Entity paths
            ClickIt.Constants.Constants.CleansingFireAltar.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.TangleAltar.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Brequel.Should().NotBeNullOrWhiteSpace();

            // Basic target strings
            ClickIt.Constants.Constants.Player.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Minion.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Boss.Should().NotBeNullOrWhiteSpace();

            // UI text
            ClickIt.Constants.Constants.Any.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.PlayerGains.Should().NotBeNullOrWhiteSpace();

            // Distinctness
            ClickIt.Constants.Constants.CleansingFireAltar.Should().NotBe(ClickIt.Constants.Constants.TangleAltar);
        }

        [TestMethod]
        public void TimingAndNumericConstants_ShouldHaveReasonableValues()
        {
            ClickIt.Constants.Constants.VerisiumHoldFailsafeMs.Should().BeInRange(1000, 30000);
            ClickIt.Constants.Constants.MouseMovementDelay.Should().BeInRange(0, 500);
            ClickIt.Constants.Constants.MouseClickDelay.Should().BeInRange(0, 500);
            ClickIt.Constants.Constants.HotkeyReleaseFailsafeMs.Should().BeInRange(1000, 30000);
            ClickIt.Constants.Constants.MaxErrorsToTrack.Should().BeGreaterThan(0);

            // Positive checks
            ClickIt.Constants.Constants.VerisiumHoldFailsafeMs.Should().BePositive();
            ClickIt.Constants.Constants.MouseMovementDelay.Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void InputEventConstants_ShouldMatchExpectedValues()
        {
            ClickIt.Constants.Constants.MouseEventLeftDown.Should().Be(0x02);
            ClickIt.Constants.Constants.MouseEventLeftUp.Should().Be(0x04);
            ClickIt.Constants.Constants.MouseEventRightDown.Should().Be(0x0008);
            ClickIt.Constants.Constants.MouseEventRightUp.Should().Be(0x0010);

            ClickIt.Constants.Constants.KeyEventExtendedKey.Should().Be(0x0001);
            ClickIt.Constants.Constants.KeyEventKeyUp.Should().Be(0x0002);
        }

        [TestMethod]
        public void UIColorConstants_ShouldHaveValidAlpha()
        {
            ClickIt.Constants.Constants.PlayerColor.W.Should().BeInRange(0f, 1f);
            ClickIt.Constants.Constants.MinionColor.W.Should().BeInRange(0f, 1f);
            ClickIt.Constants.Constants.BossColor.W.Should().BeInRange(0f, 1f);
        }

        [TestMethod]
        public void AltarModCollections_ShouldBeWellFormedAndConsistent()
        {
            // Basic non-empty expectations
            ClickIt.Constants.AltarModsConstants.UpsideMods.Should().NotBeEmpty();
            ClickIt.Constants.AltarModsConstants.DownsideMods.Should().NotBeEmpty();

            // Structure checks
            ClickIt.Constants.AltarModsConstants.UpsideMods.Should().AllSatisfy(mod =>
            {
                mod.Id.Should().NotBeNullOrWhiteSpace();
                mod.Name.Should().NotBeNullOrWhiteSpace();
                mod.Type.Should().NotBeNullOrWhiteSpace();
                mod.DefaultValue.Should().BeInRange(0, 200);
            });

            ClickIt.Constants.AltarModsConstants.DownsideMods.Should().AllSatisfy(mod =>
            {
                mod.Id.Should().NotBeNullOrWhiteSpace();
                mod.Name.Should().NotBeNullOrWhiteSpace();
                mod.Type.Should().NotBeNullOrWhiteSpace();
                mod.DefaultValue.Should().BeInRange(0, 200);
            });

            // Uniqueness of (Id, Type)
            var downsidePairs = ClickIt.Constants.AltarModsConstants.DownsideMods.Select(m => (m.Id, m.Type));
            downsidePairs.Should().OnlyHaveUniqueItems("Downside (Id,Type) pairs should be unique");

            var upsidePairs = ClickIt.Constants.AltarModsConstants.UpsideMods.Select(m => (m.Id, m.Type));
            upsidePairs.Should().OnlyHaveUniqueItems("Upside (Id,Type) pairs should be unique");

            // Filter & altar target dictionaries contain expected keys and map correctly
            ClickIt.Constants.AltarModsConstants.FilterTargetDict.Should().ContainKey("Any");
            ClickIt.Constants.AltarModsConstants.FilterTargetDict["Any"].Should().Be(ClickIt.Constants.AffectedTarget.Any);
            ClickIt.Constants.AltarModsConstants.FilterTargetDict.Should().ContainKey("Player");

            ClickIt.Constants.AltarModsConstants.AltarTargetDict.Should().ContainKey("Player gains:");
            ClickIt.Constants.AltarModsConstants.AltarTargetDict["Player gains:"].Should().Be(ClickIt.Constants.AffectedTarget.Player);

            // Essential mod presence smoke checks
            var allUpsideText = string.Join(" ", ClickIt.Constants.AltarModsConstants.UpsideMods.Select(m => m.Id + " " + m.Name));
            allUpsideText.Should().Contain("Currency").And.Contain("Map");

            var allDownsideText = string.Join(" ", ClickIt.Constants.AltarModsConstants.DownsideMods.Select(m => m.Id + " " + m.Name));
            allDownsideText.Should().Contain("Damage");

            // Boss-targeted mod presence
            ClickIt.Constants.AltarModsConstants.UpsideMods.Where(m => m.Type == "Boss").Should().NotBeEmpty();
            ClickIt.Constants.AltarModsConstants.DownsideMods.Where(m => m.Type == "Boss").Should().NotBeEmpty();

            // Reasonable size balance (non-brittle)
            var upsideCount = ClickIt.Constants.AltarModsConstants.UpsideMods.Count;
            var downsideCount = ClickIt.Constants.AltarModsConstants.DownsideMods.Count;
            (upsideCount + downsideCount).Should().BeGreaterThan(0);
        }
    }
}
