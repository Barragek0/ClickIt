using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class UltimatumSettingsRuntimeServiceTests
    {
        [TestMethod]
        public void ShouldTakeRewardForGruelingGauntletModifier_MatchesGroupedBaseModifierNames()
        {
            var settings = new global::ClickIt.ClickItSettings
            {
                UltimatumTakeRewardModifierNames = new HashSet<string>(new[] { "Ruin I" }, System.StringComparer.OrdinalIgnoreCase),
                UltimatumContinueModifierNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            };

            global::ClickIt.UltimatumSettingsRuntimeService.ShouldTakeRewardForGruelingGauntletModifier(settings, "Ruin").Should().BeTrue();
            global::ClickIt.UltimatumSettingsRuntimeService.ShouldTakeRewardForGruelingGauntletModifier(settings, "Random Modifier").Should().BeFalse();
        }
    }
}