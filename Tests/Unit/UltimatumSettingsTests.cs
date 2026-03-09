using System;
using System.Linq;
using System.Collections.Generic;
using ClickIt.Constants;
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
            foreach (var modifier in UltimatumModifiersConstants.AllModifierNames)
            {
                string description = UltimatumModifiersConstants.GetDescription(modifier);

                description.Should().NotBeNullOrWhiteSpace();
                description.Should().NotContain("\n");
                description.Length.Should().BeLessOrEqualTo(120);
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
    }
}
