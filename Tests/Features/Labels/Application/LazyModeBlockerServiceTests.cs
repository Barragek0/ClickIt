using ClickIt.Features.Labels.Application;
using ExileCore.PoEMemory.Elements;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Labels.Application
{
    [TestClass]
    public class LazyModeBlockerServiceTests
    {
        [TestMethod]
        public void HasRestrictedItemsOnScreen_ReturnsFalseAndClearsReason_WhenNoRestrictionsMatch()
        {
            var settings = new ClickItSettings();
            settings.LazyModeNormalMonsterBlockCount = 0;
            settings.LazyModeMagicMonsterBlockCount = 0;
            settings.LazyModeRareMonsterBlockCount = 0;
            settings.LazyModeUniqueMonsterBlockCount = 0;

            var service = new LazyModeBlockerService(settings, null, _ => { });
            IReadOnlyList<LabelOnGround> labels = [];

            bool result = service.HasRestrictedItemsOnScreen(labels);

            result.Should().BeFalse();
            service.LastRestrictionReason.Should().BeNull();
        }

        [TestMethod]
        public void HasRestrictedItemsOnScreen_ReturnsFalse_WhenLabelsAreNull()
        {
            var settings = new ClickItSettings();
            settings.LazyModeNormalMonsterBlockCount = 0;
            settings.LazyModeMagicMonsterBlockCount = 0;
            settings.LazyModeRareMonsterBlockCount = 0;
            settings.LazyModeUniqueMonsterBlockCount = 0;

            var service = new LazyModeBlockerService(settings, null, _ => { });

            bool result = service.HasRestrictedItemsOnScreen(null);

            result.Should().BeFalse();
            service.LastRestrictionReason.Should().BeNull();
        }
    }
}