using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
                UltimatumModifierPriority = new System.Collections.Generic.List<string>
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
        public void UltimatumPriority_PersistsOrderAcrossJsonRoundTrip()
        {
            var settings = new ClickItSettings
            {
                UltimatumModifierPriority = new System.Collections.Generic.List<string>
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

        [TestMethod]
        public void UltimatumPriority_DefaultOrder_PutsFailStateModsNearBottom()
        {
            string[] priorities = UltimatumModifiersConstants.AllModifierNames;
            int ruinIndex = Array.IndexOf(priorities, "Ruin");
            int stalkingRuinIndex = Array.IndexOf(priorities, "Stalking Ruin");
            int resistantIndex = Array.IndexOf(priorities, "Resistant Monsters");
            int deadlyIndex = Array.IndexOf(priorities, "Deadly Monsters");
            int stormcallerIndex = Array.IndexOf(priorities, "Stormcaller Runes");
            int impurityIndex = Array.IndexOf(priorities, "Impurity");
            int reducedRecoveryIndex = Array.IndexOf(priorities, "Reduced Recovery");
            int droughtIndex = Array.IndexOf(priorities, "Drought");

            resistantIndex.Should().BeGreaterOrEqualTo(0);
            droughtIndex.Should().BeLessThan(stormcallerIndex);
            reducedRecoveryIndex.Should().BeLessThan(stormcallerIndex);
            impurityIndex.Should().BeGreaterThan(stormcallerIndex);
            deadlyIndex.Should().BeGreaterThan(stormcallerIndex);
            ruinIndex.Should().BeGreaterThan(resistantIndex);
            stalkingRuinIndex.Should().BeGreaterThan(ruinIndex);
        }

        [TestMethod]
        public void UltimatumDescriptions_AreOneLineAndEasyToScan()
        {
            foreach (var modifier in UltimatumModifiersConstants.AllModifierNamesWithStages)
            {
                string description = UltimatumModifiersConstants.GetDescription(modifier);

                description.Should().NotBeNullOrWhiteSpace();
                description.Should().NotContain("\n");
                description.Length.Should().BeLessOrEqualTo(150);
            }
        }

        [TestMethod]
        public void UltimatumDescriptions_CoverAllKnownModifiers()
        {
            foreach (var modifier in UltimatumModifiersConstants.AllModifierNames)
            {
                UltimatumModifiersConstants.ModifierDescriptions.ContainsKey(modifier).Should().BeTrue();
            }
        }

        [TestMethod]
        public void UltimatumModifiersWithStages_ContainsTieredAndBaseEntries()
        {
            string[] names = UltimatumModifiersConstants.AllModifierNamesWithStages;

            names.Should().Contain("Restless Ground");
            names.Should().Contain("Restless Ground I");
            names.Should().Contain("Restless Ground IV");
            names.Should().Contain("Stormcaller Runes IV");
            names.Should().Contain("Blood Altar II");
            names.Should().Contain("Reduced Recovery II");
            names.Should().NotContain("Blood Altar III");
            names.Should().NotContain("Reduced Recovery III");
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count().Should().Be(names.Length);
        }

        [TestMethod]
        public void UltimatumTakeRewardTable_DefaultsToContinueAllAndTakeRewardNone()
        {
            var settings = new ClickItSettings();

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
            settings.ShouldTakeRewardForGruelingGauntletModifier("Deadly Monsters").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldTakeRewardForGruelingGauntletModifier_MatchesDisplayNameInsideParentheses()
        {
            var settings = new ClickItSettings
            {
                UltimatumTakeRewardModifierNames = new HashSet<string>(new[] { "Stormcaller Runes IV" }, StringComparer.OrdinalIgnoreCase),
                UltimatumContinueModifierNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            settings.ShouldTakeRewardForGruelingGauntletModifier("LightningRuneDaemon4 (Stormcaller Runes IV)").Should().BeTrue();
            settings.ShouldTakeRewardForGruelingGauntletModifier("LightningRuneDaemon4(Stormcaller Runes IV)").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldTakeRewardForGruelingGauntletModifier_MatchesTierBaseAfterParenthesesNormalization()
        {
            var settings = new ClickItSettings
            {
                UltimatumTakeRewardModifierNames = new HashSet<string>(new[] { "Stormcaller Runes" }, StringComparer.OrdinalIgnoreCase),
                UltimatumContinueModifierNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            settings.ShouldTakeRewardForGruelingGauntletModifier("LightningRuneDaemon4 (Stormcaller Runes IV)").Should().BeTrue();
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
        public void UltimatumDescriptions_TieredModifiersIncludeBaseDescriptionAndTierLabel()
        {
            string tieredDescription = UltimatumModifiersConstants.GetDescription("Blistering Cold I");

            tieredDescription.Should().Contain("Cold");
            tieredDescription.Should().Contain("Tier I");
        }

        [TestMethod]
        public void UltimatumTakeRewardSubmenuLogic_MoveToTakeReward_MarksModifierAsTakeRewardInRuntimeDecision()
        {
            var settings = new ClickItSettings();
            const string modifier = "Blistering Cold III";

            settings.UltimatumTakeRewardModifierNames.Remove(modifier);
            settings.UltimatumContinueModifierNames.Add(modifier);

            MethodInfo? moveMethod = typeof(ClickItSettings).GetMethod("MoveUltimatumTakeRewardModifier", BindingFlags.Instance | BindingFlags.NonPublic);
            moveMethod.Should().NotBeNull();
            moveMethod!.Invoke(settings, [modifier, true]);

            settings.UltimatumTakeRewardModifierNames.Should().Contain(modifier);
            settings.UltimatumContinueModifierNames.Should().NotContain(modifier);
            settings.ShouldTakeRewardForGruelingGauntletModifier(modifier).Should().BeTrue();
        }

        [TestMethod]
        public void UltimatumTakeRewardSubmenuLogic_MoveToKeepGoing_MarksModifierAsKeepGoingInRuntimeDecision()
        {
            var settings = new ClickItSettings();
            const string modifier = "Blistering Cold III";

            settings.UltimatumTakeRewardModifierNames.Add(modifier);
            settings.UltimatumContinueModifierNames.Remove(modifier);

            MethodInfo? moveMethod = typeof(ClickItSettings).GetMethod("MoveUltimatumTakeRewardModifier", BindingFlags.Instance | BindingFlags.NonPublic);
            moveMethod.Should().NotBeNull();
            moveMethod!.Invoke(settings, [modifier, false]);

            settings.UltimatumTakeRewardModifierNames.Should().NotContain(modifier);
            settings.UltimatumContinueModifierNames.Should().Contain(modifier);
            settings.ShouldTakeRewardForGruelingGauntletModifier(modifier).Should().BeFalse();
        }

        [TestMethod]
        public void UltimatumTakeRewardSubmenuLogic_TieredGroupContainsTierEntriesOnly_NotBaseName()
        {
            FieldInfo? groupsField = typeof(ClickItSettings).GetField("UltimatumModifierGroups", BindingFlags.Static | BindingFlags.NonPublic);
            groupsField.Should().NotBeNull();

            var groups = ((System.Collections.IEnumerable)groupsField!.GetValue(null)!).Cast<object>().ToList();
            object blisteringColdGroup = groups.First(group =>
                string.Equals((string)group.GetType().GetProperty("Id")!.GetValue(group)!, "Blistering Cold", StringComparison.Ordinal));

            var members = ((System.Collections.IEnumerable)blisteringColdGroup.GetType().GetProperty("Members")!.GetValue(blisteringColdGroup)!)
                .Cast<string>()
                .ToArray();

            members.Should().Contain("Blistering Cold I");
            members.Should().Contain("Blistering Cold IV");
            members.Should().NotContain("Blistering Cold");
        }

        [TestMethod]
        public void UltimatumDescriptions_ExplainKeyDangerModsClearly()
        {
            UltimatumModifiersConstants.GetDescription("Ruin").ToLowerInvariant().Should().Contain("fail");
            UltimatumModifiersConstants.GetDescription("Stalking Ruin").ToLowerInvariant().Should().Contain("fail");
            UltimatumModifiersConstants.GetDescription("Drought").ToLowerInvariant().Should().Contain("flask");
        }

        [TestMethod]
        public void UltimatumPriorityGradient_TopEntryIsBrighterThanBottomEntry()
        {
            int total = UltimatumModifiersConstants.AllModifierNames.Length;
            var topColor = UltimatumModifiersConstants.GetPriorityGradientColor(0, total, 1f);
            var bottomColor = UltimatumModifiersConstants.GetPriorityGradientColor(total - 1, total, 1f);

            float topLuminance = (0.2126f * topColor.X) + (0.7152f * topColor.Y) + (0.0722f * topColor.Z);
            float bottomLuminance = (0.2126f * bottomColor.X) + (0.7152f * bottomColor.Y) + (0.0722f * bottomColor.Z);

            topLuminance.Should().BeGreaterThan(bottomLuminance);
        }

        [TestMethod]
        public void UltimatumClickModes_DefaultToDisabled()
        {
            var settings = new ClickItSettings();

            settings.IsInitialUltimatumClickEnabled().Should().BeFalse();
            settings.IsOtherUltimatumClickEnabled().Should().BeFalse();
            settings.IsAnyUltimatumClickEnabled().Should().BeFalse();
        }

        [TestMethod]
        public void UltimatumClickModes_InitialToggleEnablesAnyMode()
        {
            var settings = new ClickItSettings();
            settings.ClickInitialUltimatum.Value = true;

            settings.IsInitialUltimatumClickEnabled().Should().BeTrue();
            settings.IsOtherUltimatumClickEnabled().Should().BeFalse();
            settings.IsAnyUltimatumClickEnabled().Should().BeTrue();
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
        public void DetailedDebugSections_DefaultToEnabled()
        {
            var settings = new ClickItSettings();

            settings.IsAnyDetailedDebugSectionEnabled().Should().BeTrue();
        }

        [TestMethod]
        public void DetailedDebugSections_ReturnFalse_WhenAllDisabled()
        {
            var settings = new ClickItSettings();

            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowUltimatum.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            settings.IsAnyDetailedDebugSectionEnabled().Should().BeFalse();
        }

        [TestMethod]
        public void DetailedDebugSections_UltimatumToggleEnablesDetailedOverlay()
        {
            var settings = new ClickItSettings();

            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowInventoryPickup.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowPathfinding.Value = false;
            settings.DebugShowUltimatum.Value = true;
            settings.DebugShowClicking.Value = false;
            settings.DebugShowRuntimeDebugLogOverlay.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            settings.IsAnyDetailedDebugSectionEnabled().Should().BeTrue();
            settings.IsOnlyPathfindingDetailedDebugSectionEnabled().Should().BeFalse();
        }
    }
}
