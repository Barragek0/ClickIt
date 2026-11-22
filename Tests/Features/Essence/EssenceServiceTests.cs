namespace ClickIt.Tests.Features.Essence
{
    [TestClass]
    public class EssenceServiceTests
    {
        [TestMethod]
        [TestCategory("Settings")]
        public void CorruptionSafeguards_AreEnabledByDefault()
        {
            var settings = new ClickItSettings();

            settings.EssenceCorruptionSkipIfDeafening.Value.Should().BeTrue();
            settings.EssenceCorruptionSkipIfSpecialEssence.Value.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Settings")]
        public void CorruptionSafeguards_KeepTheirPersistedNames()
        {
            // The renames are display-only: a settings file saved before them must still load.
            string json = JsonConvert.SerializeObject(new ClickItSettings());

            json.Should().Contain("\"EssenceSecondCorruptionOnlyWhenNoDeafening\"");
            json.Should().Contain("\"EssenceSecondCorruptionOnlyWhenNoCorruptionEssences\"");
        }

        [TestMethod]
        [TestCategory("Settings")]
        public void CorruptionSafeguards_RoundTrip_UnderTheirPersistedNames()
        {
            var settings = new ClickItSettings();
            settings.EssenceCorruptionSkipIfDeafening.Value = false;
            settings.EssenceCorruptionSkipIfSpecialEssence.Value = false;

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();
            restored!.EssenceCorruptionSkipIfDeafening.Value.Should().BeFalse();
            restored.EssenceCorruptionSkipIfSpecialEssence.Value.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCorruptEssence_ReturnsFalse_WhenLabelIsNull()
        {
            var settings = new ClickItSettings();
            var service = new EssenceService(settings);

            service.ShouldCorruptEssence(null).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void IsBlockedBySafeguard_SkipsADeafeningEssence()
        {
            var settings = new ClickItSettings();

            EssenceService.IsBlockedBySafeguard(settings, Adapter("Deafening Essence of Misery")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void IsBlockedBySafeguard_SkipsASpecialEssence()
        {
            var settings = new ClickItSettings();

            EssenceService.IsBlockedBySafeguard(settings, Adapter("Essence of Hysteria")).Should().BeTrue();
            EssenceService.IsBlockedBySafeguard(settings, Adapter("Essence of Trickery")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void IsBlockedBySafeguard_AllowsAnOrdinaryEssence()
        {
            var settings = new ClickItSettings();

            EssenceService.IsBlockedBySafeguard(settings, Adapter("Screaming Essence of Misery")).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsBlockedBySafeguard_DoesNotSkipWhenItsSettingIsOff()
        {
            var settings = new ClickItSettings();
            settings.EssenceCorruptionSkipIfDeafening.Value = false;
            settings.EssenceCorruptionSkipIfSpecialEssence.Value = false;

            EssenceService.IsBlockedBySafeguard(settings, Adapter("Deafening Essence of Misery")).Should().BeFalse();
            EssenceService.IsBlockedBySafeguard(settings, Adapter("Essence of Hysteria")).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsBlockedBySafeguard_DoesNotRejectAMissingLabel()
        {
            var settings = new ClickItSettings();

            EssenceService.IsBlockedBySafeguard(settings, null).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsBlockedBySafeguard_MatchesTextInAChildElement()
        {
            var settings = new ClickItSettings();
            var root = new ElementAdapterStub("Essence");
            root.AddChild(new ElementAdapterStub("Deafening Essence of Misery"));

            EssenceService.IsBlockedBySafeguard(settings, root).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void Safeguards_ApplyToTheFirstCorruptionToo()
        {
            var settings = new ClickItSettings();
            settings.CorruptAllEssences.Value = true;
            var service = new EssenceService(settings);

            // The safeguard must not wait for a second corruption attempt.
            IsBlocked(settings, "Deafening Essence of Misery").Should().BeTrue();
            IsBlocked(settings, "Essence of Delirium").Should().BeTrue();
            IsBlocked(settings, "Screaming Essence of Misery").Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void Safeguards_ApplyToTheSecondCorruptionAsWell()
        {
            var settings = new ClickItSettings();
            settings.CorruptAllEssences.Value = true;

            IsBlocked(settings, "Deafening Essence of Misery").Should().BeTrue();
            IsBlocked(settings, "Essence of Insanity").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void SecondCorruption_StillAllowsAnAlreadyCorruptedLabel()
        {
            var settings = new ClickItSettings();
            settings.CorruptAllEssences.Value = true;

            // The second corruption deliberately revisits a corrupted essence, so the table selection must ignore
            // "Corrupted"; the safeguards are applied separately, by IsBlockedBySafeguard.
            EssenceService.IsSelectedForCorruption(settings, Adapter("Corrupted Screaming Essence of Misery"))
                .Should().BeTrue();
            EssenceService.IsBlockedBySafeguard(settings, Adapter("Corrupted Screaming Essence of Misery"))
                .Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsSelectedForCorruption_FollowsTheTableWhenCorruptAllIsOff()
        {
            var settings = new ClickItSettings();

            EssenceService.IsSelectedForCorruption(settings, Adapter("Screaming Essence of Misery")).Should().BeTrue();
            EssenceService.IsSelectedForCorruption(settings, Adapter("Essence of Hysteria")).Should().BeFalse();
        }

        private static bool IsBlocked(ClickItSettings settings, string text)
            => EssenceService.IsBlockedBySafeguard(settings, Adapter(text));

        private static IElementAdapter Adapter(string text) => new ElementAdapterStub(text);
    }
}
