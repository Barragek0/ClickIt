using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Constants;
using System.Linq;
using System;

namespace ClickIt.Tests.Constants
{
    [TestClass]
    [TestCategory("Unit")]
    public class ConstantsValidationTests
    {
        // Merged from MergedConstantsTests.cs
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
            ClickIt.Constants.Constants.MouseEventRightDown.Should().Be(0x08);
            ClickIt.Constants.Constants.MouseEventRightUp.Should().Be(0x10);
        }

        // Merged from AltarModsConstantsIntegrityTests.cs
        [TestMethod]
        public void UpsideAndDownside_ShouldHaveNoDuplicateCompositeIdTypePairs()
        {
            var all = AltarModsConstants.UpsideMods.Concat(AltarModsConstants.DownsideMods).ToList();
            var compositeKeys = all.Select(t => ($"{t.Type}|{t.Id}").ToLower()).ToList();
            compositeKeys.Should().OnlyHaveUniqueItems();
        }

        [TestMethod]
        public void ModEntries_ShouldHaveNonEmptyNamesAndValidTypes()
        {
            var validTypes = new[] { "Minion", "Boss", "Player" };
            foreach (var (Id, Name, Type, _) in AltarModsConstants.UpsideMods.Concat(AltarModsConstants.DownsideMods))
            {
                Id.Should().NotBeNullOrWhiteSpace();
                Name.Should().NotBeNullOrWhiteSpace();
                Type.Should().NotBeNullOrWhiteSpace();
                validTypes.Should().Contain(Type);
            }
        }

        [TestMethod]
        public void Downside_DefaultWeights_ShouldBeWithin1To100()
        {
            foreach (var (_, _, _, defaultWeight) in AltarModsConstants.DownsideMods)
            {
                defaultWeight.Should().BeInRange(1, 100);
            }
        }

        [DataTestMethod]
        [DataRow("Any")]
        [DataRow("Player")]
        [DataRow("Minions")]
        [DataRow("Boss")]
        public void FilterTargetDict_ShouldContainKey(string key)
        {
            AltarModsConstants.FilterTargetDict.Should().ContainKey(key);
        }

        // Merged from ConstantsIntegrityTests.cs
        [TestMethod]
        public void Constants_ShouldHaveValidStringValues()
        {
            // Entity paths
            ClickIt.Constants.Constants.CleansingFireAltar.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.TangleAltar.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Brequel.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.CrimsonIron.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.CopperAltar.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.PetrifiedWood.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Bismuth.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Verisium.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.ClosedDoorPast.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.LegionInitiator.Should().NotBeNullOrWhiteSpace();

            // Target types
            ClickIt.Constants.Constants.Player.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Minion.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Boss.Should().NotBeNullOrWhiteSpace();

            // UI text
            ClickIt.Constants.Constants.Any.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Minions.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.BossDrops.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.PlayerGains.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.EldritchMinionsGain.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.MapBossGains.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.MapBoss.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void Constants_ShouldHaveReasonableTimingValues()
        {
            ClickIt.Constants.Constants.VerisiumHoldFailsafeMs.Should().BeInRange(1000, 30000);
            ClickIt.Constants.Constants.MouseMovementDelay.Should().BeInRange(0, 500);
            ClickIt.Constants.Constants.MouseClickDelay.Should().BeInRange(0, 500);
            ClickIt.Constants.Constants.HotkeyReleaseFailsafeMs.Should().BeInRange(1000, 30000);
            ClickIt.Constants.Constants.MaxErrorsToTrack.Should().BeGreaterThan(0);
        }
    }
}