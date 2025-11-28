using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceCreateClickSettingsTests
    {
        [TestMethod]
        public void CreateClickSettings_UsesSettingsValues_WhenNoLabelsProvided()
        {
            var settings = new ClickItSettings();
            var ess = new EssenceService(settings);
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });

            var svc = new LabelFilterService(settings, ess, err, null);

            // Avoid native Win32 key queries in tests by overriding the static provider via reflection
            var ksField = typeof(LabelFilterService).GetField("KeyStateProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            ksField.SetValue(null, new Func<System.Windows.Forms.Keys, bool>((k) => false));

            // Short-circuit the lazy-mode restricted check in tests so we don't need to manipulate ExileCore.Memory objects
            // LazyModeRestrictedChecker is a static property - assign directly in tests for determinism
            LabelFilterService.LazyModeRestrictedChecker = (svc2, labels) => true;

            // CreateClickSettings is private - invoke via reflection
            var method = typeof(LabelFilterService).GetMethod("CreateClickSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Should().NotBeNull();

            var result = method.Invoke(svc, [null]);
            result.Should().NotBeNull();

            // ClickSettings is a private nested type - reflect its properties to assert mapping
            var clickSettingsType = result.GetType();

            int clickDistance = (int)clickSettingsType.GetProperty("ClickDistance").GetValue(result)!;
            bool clickLeagueChests = (bool)clickSettingsType.GetProperty("ClickLeagueChests").GetValue(result)!;
            bool clickSettlersOre = (bool)clickSettingsType.GetProperty("ClickSettlersOre").GetValue(result)!;

            clickDistance.Should().Be(settings.ClickDistance.Value);
            clickLeagueChests.Should().Be(settings.ClickLeagueChests.Value);
            clickSettlersOre.Should().Be(settings.ClickSettlersOre.Value);
        }

        [TestMethod]
        public void CreateClickSettings_DisablesLeagueChestsAndSettlers_WhenLazyModeAndRestrictedItemOnScreen()
        {
            var settings = new ClickItSettings();
            // enable lazy mode
            settings.LazyMode.Value = true;
            settings.ClickLeagueChests.Value = true;
            settings.ClickSettlersOre.Value = true;

            var ess = new EssenceService(settings);
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            var svc = new LabelFilterService(settings, ess, err, null);

            // Build a fake LabelOnGround with a fake Entity.Path containing PetrifiedWood and small DistancePlayer
            var labelType = typeof(ExileCore.PoEMemory.Elements.LabelOnGround);
            var label = (ExileCore.PoEMemory.Elements.LabelOnGround)RuntimeHelpers.GetUninitializedObject(labelType);

            // No need to populate the entity - the LazyModeRestrictedChecker is overridden to return true

            var list = new List<ExileCore.PoEMemory.Elements.LabelOnGround> { label };

            // Invoke private CreateClickSettings
            var method = typeof(LabelFilterService).GetMethod("CreateClickSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = method.Invoke(svc, [list]);

            var clickSettingsType = result.GetType();

            bool clickLeagueChests = (bool)clickSettingsType.GetProperty("ClickLeagueChests").GetValue(result)!;
            bool clickSettlersOre = (bool)clickSettingsType.GetProperty("ClickSettlersOre").GetValue(result)!;

            // Since lazy mode is active, and we placed a restricted item (PetrifiedWood) within click distance,
            // CreateClickSettings should apply lazy-mode restrictions and disable both ClickLeagueChests and ClickSettlersOre.
            clickLeagueChests.Should().BeFalse();
            clickSettlersOre.Should().BeFalse();
        }
    }
}
