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

            // Avoid native Win32 key queries in tests by overriding the key-state seam
            LabelFilterService.KeyStateProvider = (k) => false;

            LabelFilterService.LazyModeRestrictedChecker = (svc2, labels) => true;

            var method = typeof(LabelFilterService).GetMethod("CreateClickSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Should().NotBeNull();

            var result = method!.Invoke(svc, [null]);
            result.Should().NotBeNull();

            var clickSettingsType = result!.GetType();

            var clickDistanceProperty = clickSettingsType.GetProperty("ClickDistance");
            var clickLeagueChestsProperty = clickSettingsType.GetProperty("ClickLeagueChests");
            var clickSettlersOreProperty = clickSettingsType.GetProperty("ClickSettlersOre");
            var clickLabyrinthTrialsProperty = clickSettingsType.GetProperty("ClickLabyrinthTrials");
            clickDistanceProperty.Should().NotBeNull();
            clickLeagueChestsProperty.Should().NotBeNull();
            clickSettlersOreProperty.Should().NotBeNull();
            clickLabyrinthTrialsProperty.Should().NotBeNull();

            int clickDistance = (int)clickDistanceProperty!.GetValue(result)!;
            bool clickLeagueChests = (bool)clickLeagueChestsProperty!.GetValue(result)!;
            bool clickSettlersOre = (bool)clickSettlersOreProperty!.GetValue(result)!;
            bool clickLabyrinthTrials = (bool)clickLabyrinthTrialsProperty!.GetValue(result)!;

            clickDistance.Should().Be(settings.ClickDistance.Value);
            clickLeagueChests.Should().Be(settings.ClickLeagueChests.Value);
            clickSettlersOre.Should().Be(settings.ClickSettlersOre.Value);
            clickLabyrinthTrials.Should().Be(settings.ClickLabyrinthTrials.Value);
        }

        [TestMethod]
        public void CreateClickSettings_DisablesLeagueChestsAndSettlers_WhenLazyModeAndRestrictedItemOnScreen()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickLeagueChests.Value = true;
            settings.ClickSettlersOre.Value = true;

            LabelFilterService.KeyStateProvider = (k) => false;
            LabelFilterService.LazyModeRestrictedChecker = (svc2, labels) => true;

            var ess = new EssenceService(settings);
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            var svc = new LabelFilterService(settings, ess, err, null);

            var labelType = typeof(ExileCore.PoEMemory.Elements.LabelOnGround);
            var label = (ExileCore.PoEMemory.Elements.LabelOnGround)RuntimeHelpers.GetUninitializedObject(labelType);


            var list = new List<ExileCore.PoEMemory.Elements.LabelOnGround> { label };

            var method = typeof(LabelFilterService).GetMethod("CreateClickSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = method!.Invoke(svc, [list]);
            result.Should().NotBeNull();

            var clickSettingsType = result!.GetType();

            var clickLeagueChestsProperty = clickSettingsType.GetProperty("ClickLeagueChests");
            var clickSettlersOreProperty = clickSettingsType.GetProperty("ClickSettlersOre");
            clickLeagueChestsProperty.Should().NotBeNull();
            clickSettlersOreProperty.Should().NotBeNull();

            bool clickLeagueChests = (bool)clickLeagueChestsProperty!.GetValue(result)!;
            bool clickSettlersOre = (bool)clickSettlersOreProperty!.GetValue(result)!;

            clickLeagueChests.Should().BeFalse();
            clickSettlersOre.Should().BeFalse();
        }

        [TestMethod]
        public void CreateClickSettings_KeepsSettlersEnabled_WhenLazyModeAndNoRestrictedItemOnScreen()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickSettlersOre.Value = true;

            var ess = new EssenceService(settings);
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            var svc = new LabelFilterService(settings, ess, err, null);

            LabelFilterService.KeyStateProvider = (k) => false;
            LabelFilterService.LazyModeRestrictedChecker = (svc2, labels) => false;

            var method = typeof(LabelFilterService).GetMethod("CreateClickSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Should().NotBeNull();

            var result = method!.Invoke(svc, [null]);
            result.Should().NotBeNull();

            var clickSettingsType = result!.GetType();
            var clickSettlersOreProperty = clickSettingsType.GetProperty("ClickSettlersOre");
            clickSettlersOreProperty.Should().NotBeNull();

            bool clickSettlersOre = (bool)clickSettlersOreProperty!.GetValue(result)!;
            clickSettlersOre.Should().BeTrue();
        }
    }
}
