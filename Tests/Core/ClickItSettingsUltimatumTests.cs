using System;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Definitions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class UltimatumSettingsTests
    {
        [TestMethod]
        public void UltimatumPriority_DefaultsToAllKnownModifiers()
        {
            var settings = new ClickItSettings();

            var priorities = settings.GetUltimatumModifierPriority();

            priorities.Should().HaveCount(UltimatumModifiersConstants.AllModifierNames.Length);
            priorities.Should().Contain("Choking Miasma");
            priorities.Should().Contain("Ruin");
            priorities.Should().Contain("Stormcaller Runes");
        }

        [TestMethod]
        public void UltimatumPriority_SanitizesUnknownAndDuplicateEntries()
        {
            var settings = new ClickItSettings
            {
                UltimatumModifierPriority = new List<string>
                {
                    "Choking Miasma",
                    "Unknown Modifier",
                    "Choking Miasma"
                }
            };

            var priorities = settings.GetUltimatumModifierPriority();

            priorities.Should().Contain("Choking Miasma");
            priorities.Should().NotContain("Unknown Modifier");
            priorities.Count.Should().Be(priorities.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [TestMethod]
        public void UltimatumTakeRewardTable_DefaultsToContinueAllAndTakeRewardNone()
        {
            var settings = new ClickItSettings();

            settings.ClickUltimatumTakeRewardButton.Value.Should().BeTrue();
            settings.ShowUltimatumTakeRewardModifierTablePanel.Should().BeTrue();
            settings.GetUltimatumTakeRewardModifierNames().Should().BeEmpty();
            settings.UltimatumContinueModifierNames.Should().HaveCount(UltimatumModifiersConstants.AllModifierNamesWithStages.Length);
        }

        [TestMethod]
        public void ShouldTakeRewardForGruelingGauntletModifier_MatchesExactAndTieredBaseEntries()
        {
            var settings = new ClickItSettings
            {
                UltimatumTakeRewardModifierNames = new HashSet<string>(new[] { "Restless Ground", "Ruin II" }, StringComparer.OrdinalIgnoreCase),
                UltimatumContinueModifierNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            settings.ShouldTakeRewardForGruelingGauntletModifier("Restless Ground IV").Should().BeTrue();
            settings.ShouldTakeRewardForGruelingGauntletModifier("Ruin II").Should().BeTrue();
            settings.ShouldTakeRewardForGruelingGauntletModifier("Ruin IV").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldTakeRewardForGruelingGauntletModifier_MatchesBaseInGameNameToTierOneTableEntry()
        {
            var settings = new ClickItSettings
            {
                UltimatumTakeRewardModifierNames = new HashSet<string>(new[] { "Ruin I" }, StringComparer.OrdinalIgnoreCase),
                UltimatumContinueModifierNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            settings.ShouldTakeRewardForGruelingGauntletModifier("Ruin").Should().BeTrue();
            settings.ShouldTakeRewardForGruelingGauntletModifier("Stalking Ruin").Should().BeFalse();
        }

        [TestMethod]
        public void UltimatumClickModes_SplitTogglesCanBeEnabledIndependently()
        {
            var settings = new ClickItSettings();

            settings.ClickInitialUltimatum.Value = true;
            settings.IsInitialUltimatumClickEnabled().Should().BeTrue();
            settings.IsOtherUltimatumClickEnabled().Should().BeFalse();

            settings.ClickInitialUltimatum.Value = false;
            settings.ClickUltimatumChoices.Value = true;
            settings.IsInitialUltimatumClickEnabled().Should().BeFalse();
            settings.IsOtherUltimatumClickEnabled().Should().BeTrue();
        }

        [TestMethod]
        public void UltimatumPriority_PersistsOrderAcrossJsonRoundTrip()
        {
            var settings = new ClickItSettings
            {
                UltimatumModifierPriority = new List<string>
                {
                    "Ruin",
                    "Choking Miasma",
                    "Stormcaller Runes"
                }
            };

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();
            var priorities = restored!.GetUltimatumModifierPriority();
            priorities[0].Should().Be("Ruin");
            priorities[1].Should().Be("Choking Miasma");
            priorities[2].Should().Be("Stormcaller Runes");
        }
    }
}
