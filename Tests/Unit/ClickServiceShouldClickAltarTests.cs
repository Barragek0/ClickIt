using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using ClickIt.Components;
using ClickIt.Tests.TestUtils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceShouldClickAltarTests
    {
        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenAltarTypeIsNotEnabled()
        {
            var svc = (Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));
            // Provide minimal settings + error handler to avoid DebugLog NRE when method reaches logging
            var settingsField = typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var errorField = typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            settingsField.Should().NotBeNull();
            errorField.Should().NotBeNull();
            settingsField!.SetValue(svc, new ClickItSettings());
            errorField!.SetValue(svc, new global::ClickIt.Utils.ErrorHandler(new ClickItSettings(), (s, f) => { }, (s, f) => { }));

            var primary = TestBuilders.BuildPrimary();

            svc.ShouldClickAltar(primary, clickEater: false, clickExarch: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenAltarIsNotValidCached()
        {
            var svc = (Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));
            // Provide minimal settings + error handler to avoid DebugLog NRE when method reaches logging
            var settingsField2 = typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var errorField2 = typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            settingsField2.Should().NotBeNull();
            errorField2.Should().NotBeNull();
            settingsField2!.SetValue(svc, new ClickItSettings());
            errorField2!.SetValue(svc, new global::ClickIt.Utils.ErrorHandler(new ClickItSettings(), (s, f) => { }, (s, f) => { }));

            var top = TestBuilders.BuildSecondary();
            var bottom = TestBuilders.BuildSecondary();
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);
            var primary = new PrimaryAltarComponent(AltarType.EaterOfWorlds, top, topButton, bottom, bottomButton);

            svc.ShouldClickAltar(primary, clickEater: true, clickExarch: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_ReturnsFalse_WhenCachedValidButElementsInvalid()
        {
            var svc = (Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));
            // Provide minimal settings + error handler to avoid DebugLog NRE when method reaches logging
            var settingsField3 = typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var errorField3 = typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            settingsField3.Should().NotBeNull();
            errorField3.Should().NotBeNull();
            settingsField3!.SetValue(svc, new ClickItSettings());
            errorField3!.SetValue(svc, new global::ClickIt.Utils.ErrorHandler(new ClickItSettings(), (s, f) => { }, (s, f) => { }));

            var top = TestBuilders.BuildSecondary();
            var bottom = TestBuilders.BuildSecondary();
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);
            var primary = new PrimaryAltarComponent(AltarType.EaterOfWorlds, top, topButton, bottom, bottomButton);

            var cacheField = typeof(PrimaryAltarComponent).GetField("_isValidCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var timeField = typeof(PrimaryAltarComponent).GetField("_lastValidationTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            cacheField.Should().NotBeNull();
            timeField.Should().NotBeNull();
            cacheField!.SetValue(primary, (bool?)true);
            timeField!.SetValue(primary, long.MaxValue);

            svc.ShouldClickAltar(primary, clickEater: true, clickExarch: false).Should().BeFalse();
        }
    }
}
