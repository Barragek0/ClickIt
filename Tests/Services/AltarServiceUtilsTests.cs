using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class AltarServiceUtilsTests
    {
        private static MethodInfo GetPrivateStatic(string name)
        {
            var type = Type.GetType("ClickIt.Services.AltarService, ClickIt");
            type.Should().NotBeNull();
            var m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull($"Private static method {name} should exist");
            return m;
        }



        [TestMethod]
        public void GetLine_And_CountLines_WorkAsExpected()
        {
            var getLine = GetPrivateStatic("GetLine");
            var countLines = GetPrivateStatic("CountLines");

            string text = "first\nsecond\nthird";
            var line0 = getLine.Invoke(null, new object[] { text, 0 });
            var line1 = getLine.Invoke(null, new object[] { text, 1 });
            var line2 = getLine.Invoke(null, new object[] { text, 2 });

            line0.Should().Be("first");
            line1.Should().Be("second");
            line2.Should().Be("third");

            var cnt = countLines.Invoke(null, new object[] { text });
            ((int)cnt).Should().Be(3);
        }

        [TestMethod]
        public void GetModTarget_ReturnsExpectedStrings()
        {
            var getModTarget = GetPrivateStatic("GetModTarget");

            getModTarget.Invoke(null, new object[] { "Mapboss" }).Should().Be("Boss");
            getModTarget.Invoke(null, new object[] { "EldritchMinions" }).Should().Be("Minion");
            getModTarget.Invoke(null, new object[] { "Player" }).Should().Be("Player");
            getModTarget.Invoke(null, new object[] { "SomethingElse" }).Should().Be(string.Empty);
        }

        [TestMethod]
        public void TryMatchMod_Finds_Known_Mod()
        {
            var tryMatch = GetPrivateStatic("TryMatchMod");

            // Use a known downside mod string and negativeModType that maps to Player
            string mod = "Projectiles are fired in random directions";
            string negativeModType = "Player";

            object[] args = new object[] { mod, negativeModType, null, null };
            var result = (bool)tryMatch.Invoke(null, args);
            result.Should().BeTrue();
            args[2].Should().BeOfType<bool>();
            args[3].Should().BeOfType<string>();
            ((string)args[3]).ToLowerInvariant().Should().Contain("projectiles");
        }

        [TestMethod]
        public void TryMatchMod_ReturnsFalse_ForUnknownMod()
        {
            var tryMatch = GetPrivateStatic("TryMatchMod");
            string mod = "some unknown mod text";
            string negativeModType = "Player";
            object[] args = new object[] { mod, negativeModType, null, null };
            var result = (bool)tryMatch.Invoke(null, args);
            result.Should().BeFalse();
            args[3].Should().NotBeNull();
            ((string)args[3]).Should().BeEmpty();
        }

        [TestMethod]
        public void CleanAltarModsText_RemovesRgbAndPlaceholders()
        {
            var type = Type.GetType("ClickIt.Services.AltarService, ClickIt");
            type.Should().NotBeNull();

            var instance = FormatterServices.GetUninitializedObject(type);
            // initialize the private _textCleanCache field to a real dictionary
            var cacheField = type.GetField("_textCleanCache", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheField.Should().NotBeNull();
            cacheField.SetValue(instance, new Dictionary<string, string>());

            var cleanMethod = type.GetMethod("CleanAltarModsText", BindingFlags.NonPublic | BindingFlags.Instance);
            cleanMethod.Should().NotBeNull();

            string input = "<rgb(12,34,56)>{some} <valuedefault> gain: Test";
            var cleaned = (string)cleanMethod.Invoke(instance, new object[] { input });
            cleaned.Should().NotContain("<rgb(");
            cleaned.Should().NotContain("<valuedefault>");
            cleaned.Should().NotContain("{");
            cleaned.Should().NotContain("}");
            cleaned.Should().NotContain(" "); // spaces removed by method
            cleaned.Should().Contain("Test");
        }
    }
}
