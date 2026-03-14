using System;
using System.Collections.Generic;
using System.Reflection;
using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceUltimatumPriorityTests
    {
        private static int InvokeGetModifierPriorityIndex(string modifier, IReadOnlyList<string> priorities)
        {
            var method = typeof(ClickService).GetMethod("GetModifierPriorityIndex", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return (int)method!.Invoke(null, new object[] { modifier, priorities })!;
        }

        private static bool InvokeIsUltimatumPath(string path)
        {
            var method = typeof(ClickService).GetMethod("IsUltimatumPath", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return (bool)method!.Invoke(null, new object[] { path })!;
        }

        [TestMethod]
        public void GetModifierPriorityIndex_MatchesTieredNameByPrefix()
        {
            var priorities = new List<string> { "Ruin", "Choking Miasma", "Stormcaller Runes" };

            int index = InvokeGetModifierPriorityIndex("Choking Miasma IV", priorities);

            index.Should().Be(1);
        }

        [TestMethod]
        public void GetModifierPriorityIndex_ReturnsMaxValue_WhenNoMatch()
        {
            var priorities = new List<string> { "Ruin", "Choking Miasma" };

            int index = InvokeGetModifierPriorityIndex("Completely Unknown Modifier", priorities);

            index.Should().Be(int.MaxValue);
        }

        [TestMethod]
        public void TryExtractElement_ReturnsFalse_ForNonElementObject()
        {
            var method = typeof(ClickService).GetMethod("TryExtractElement", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            object[] args = [new ThrowingGetterStub(), null!];

            bool success = (bool)method!.Invoke(null, args)!;

            success.Should().BeFalse();
            args[1].Should().BeNull();
        }

        [TestMethod]
        public void IsUltimatumPath_MatchesCaseInsensitiveUltimatumInteractablePath()
        {
            bool matched = InvokeIsUltimatumPath("metadata/terrain/leagues/ultimatum/objects/UltimatumChallengeInteractable");
            bool nonMatch = InvokeIsUltimatumPath("Metadata/Terrain/Leagues/Ritual/Objects/RitualRuneInteractable");

            matched.Should().BeTrue();
            nonMatch.Should().BeFalse();
        }

        [TestMethod]
        public void UltimatumPostBeginAdditionalClickDelay_HasExpectedValue()
        {
            var field = typeof(ClickService).GetField("UltimatumPostBeginAdditionalClickDelayMs", BindingFlags.NonPublic | BindingFlags.Static);
            field.Should().NotBeNull();

            int value = (int)field!.GetValue(null)!;
            value.Should().Be(200);
        }

        private sealed class ThrowingGetterStub
        {
            public object Dangerous => throw new NullReferenceException("Simulated transient UI read failure");
        }
    }
}
