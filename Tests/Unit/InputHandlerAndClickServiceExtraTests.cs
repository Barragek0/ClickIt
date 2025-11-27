using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using ExileCore.PoEMemory;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerAndClickServiceExtraTests
    {
        [TestMethod]
        public void IsClickHotkeyPressed_LazyMode_AllowsWhenNoRestrictedLabels()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;

            var perf = new PerformanceMonitor(settings);
            var ih = new InputHandler(settings, perf, null);

            // No cached labels and no labelFilterService -> should treat as not restricted
            var result = ih.IsClickHotkeyPressed(null, null);

            // With lazy mode enabled and no hotkeys pressed, the method returns true (lazy mode lets clicks through)
            result.Should().BeTrue();
        }

        [TestMethod]
        public void CanClick_ReturnsFalse_WhenGameControllerIsNull()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var ih = new InputHandler(settings, perf, null);

            ih.CanClick(null).Should().BeFalse();
        }

        [TestMethod]
        public void ClickService_ShouldClickAltar_RespectsSettingsAndValidity()
        {
            // Create an uninitialized ClickService and set minimal fields
            var svc = (Services.ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));
            var settings = new ClickItSettings();
            // Enable clicking for both altar types
            settings.ClickEater.Value = true;
            settings.ClickExarch.Value = true;

            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });

            typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, err);

            // Build a primary component that is valid: fill secondary components with element placeholders
            var elTop = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));
            var elBottom = (Element)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Element));

            var topMods = new ClickIt.Components.SecondaryAltarComponent(elTop, new List<string>{"top_mod"}, new List<string>{"top_down"});
            var bottomMods = new ClickIt.Components.SecondaryAltarComponent(elBottom, new List<string>{"bot_mod"}, new List<string>{"bot_down"});
            var primary = new ClickIt.Components.PrimaryAltarComponent(ClickIt.ClickIt.AltarType.EaterOfWorlds, topMods, new ClickIt.Components.AltarButton(elTop), bottomMods, new ClickIt.Components.AltarButton(elBottom));

            // Force internal valid cache on primary so ClickService will consider it cache-valid
            var cacheField = typeof(ClickIt.Components.PrimaryAltarComponent).GetField("_isValidCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var timeField = typeof(ClickIt.Components.PrimaryAltarComponent).GetField("_lastValidationTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            cacheField!.SetValue(primary, (bool?)true);
            timeField!.SetValue(primary, long.MaxValue);

            // ShouldClickAltar should return true for Eater when clickEater is enabled
            svc.ShouldClickAltar(primary, clickEater: true, clickExarch: false).Should().BeTrue();
        }
    }
}
