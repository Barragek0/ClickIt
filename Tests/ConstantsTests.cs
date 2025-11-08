using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests
{
    [TestClass]
    public class ConstantsTests
    {
        [TestMethod]
        public void EntityPathConstants_ShouldBeValidStrings()
        {
            // Test altar paths
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
        }

        [TestMethod]
        public void TargetTypeConstants_ShouldBeValidStrings()
        {
            ClickIt.Constants.Constants.Player.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Minion.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Boss.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void UITextConstants_ShouldBeValidStrings()
        {
            ClickIt.Constants.Constants.Any.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.Minions.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.BossDrops.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.PlayerGains.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.EldritchMinionsGain.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.MapBossGains.Should().NotBeNullOrWhiteSpace();
            ClickIt.Constants.Constants.MapBoss.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void TimingConstants_ShouldHaveReasonableValues()
        {
            ClickIt.Constants.Constants.VerisiumHoldFailsafeMs.Should().BeInRange(1000, 30000,
                "verisium hold failsafe should be reasonable");

            ClickIt.Constants.Constants.MouseMovementDelay.Should().BeInRange(1, 100,
                "mouse movement delay should be reasonable");

            ClickIt.Constants.Constants.MouseClickDelay.Should().BeInRange(1, 100,
                "mouse click delay should be reasonable");

            ClickIt.Constants.Constants.HotkeyReleaseFailsafeMs.Should().BeInRange(1000, 30000,
                "hotkey release failsafe should be reasonable");

            ClickIt.Constants.Constants.MaxErrorsToTrack.Should().BeInRange(1, 100,
                "max errors to track should be reasonable");
        }

        [TestMethod]
        public void TimingConstants_ShouldBePositive()
        {
            ClickIt.Constants.Constants.VerisiumHoldFailsafeMs.Should().BePositive();
            ClickIt.Constants.Constants.MouseMovementDelay.Should().BePositive();
            ClickIt.Constants.Constants.MouseClickDelay.Should().BePositive();
            ClickIt.Constants.Constants.HotkeyReleaseFailsafeMs.Should().BePositive();
            ClickIt.Constants.Constants.MaxErrorsToTrack.Should().BePositive();
        }

        [TestMethod]
        public void MouseEventConstants_ShouldHaveValidValues()
        {
            ClickIt.Constants.Constants.MouseEventLeftDown.Should().Be(0x02);
            ClickIt.Constants.Constants.MouseEventLeftUp.Should().Be(0x04);
            ClickIt.Constants.Constants.MouseEventMidDown.Should().Be(0x0020);
            ClickIt.Constants.Constants.MouseEventMidUp.Should().Be(0x0040);
            ClickIt.Constants.Constants.MouseEventRightDown.Should().Be(0x0008);
            ClickIt.Constants.Constants.MouseEventRightUp.Should().Be(0x0010);
            ClickIt.Constants.Constants.MouseEventWheel.Should().Be(0x800);
        }

        [TestMethod]
        public void KeyboardEventConstants_ShouldHaveValidValues()
        {
            ClickIt.Constants.Constants.KeyEventExtendedKey.Should().Be(0x0001);
            ClickIt.Constants.Constants.KeyEventKeyUp.Should().Be(0x0002);
            ClickIt.Constants.Constants.KeyPressed.Should().Be(0x8000);
            ClickIt.Constants.Constants.KeyToggled.Should().Be(0x0001);
        }

        [TestMethod]
        public void UIColors_ShouldHaveValidAlphaValues()
        {
            ClickIt.Constants.Constants.PlayerColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
            ClickIt.Constants.Constants.MinionColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
            ClickIt.Constants.Constants.BossColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
            ClickIt.Constants.Constants.DefaultColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
            ClickIt.Constants.Constants.BossBackgroundColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
            ClickIt.Constants.Constants.PlayerBackgroundColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
            ClickIt.Constants.Constants.MinionBackgroundColor.W.Should().BeInRange(0f, 1f, "alpha should be valid");
        }

        [TestMethod]
        public void AltarEntityPaths_ShouldBeDistinct()
        {
            ClickIt.Constants.Constants.CleansingFireAltar.Should().NotBe(ClickIt.Constants.Constants.TangleAltar,
                "altar paths should be distinct");
        }
    }
}