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
            var settings = new ClickItSettings
            {
                UltimatumTakeRewardModifierNames = new HashSet<string>(new[] { "Ruin I" }, System.StringComparer.OrdinalIgnoreCase),
                UltimatumContinueModifierNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            };

            UltimatumSettingsRuntimeService.ShouldTakeRewardForGruelingGauntletModifier(settings, "Ruin").Should().BeTrue();
            UltimatumSettingsRuntimeService.ShouldTakeRewardForGruelingGauntletModifier(settings, "Random Modifier").Should().BeFalse();
        }
    }
}