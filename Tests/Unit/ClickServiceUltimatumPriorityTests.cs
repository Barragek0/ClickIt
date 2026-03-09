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

        private static bool InvokeShouldPrimeUltimatumGroundLabelTooltips(IReadOnlyList<string> modifierNames, IReadOnlyList<string> priorities)
        {
            var method = typeof(ClickService).GetMethod("ShouldPrimeUltimatumGroundLabelTooltips", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return (bool)method!.Invoke(null, new object[] { modifierNames, priorities })!;
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
        public void TryGetPropertyValue_ReturnsFalse_WhenGetterThrows()
        {
            var method = typeof(ClickService).GetMethod("TryGetPropertyValue", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            object[] args = [new ThrowingGetterStub(), "Dangerous", null!];

            bool success = (bool)method!.Invoke(null, args)!;

            success.Should().BeFalse();
            args[2].Should().BeNull();
        }

        [TestMethod]
        public void GetUltimatumPreHoverIndices_ReturnsFirstThree_WhenMoreThanThreeOptions()
        {
            int[] indices = ClickServiceSeams.GetUltimatumPreHoverIndices(5);

            indices.Should().Equal(0, 1, 2);
        }

        [TestMethod]
        public void GetUltimatumPreHoverIndices_ReturnsAllAvailable_WhenThreeOrFewerOptions()
        {
            ClickServiceSeams.GetUltimatumPreHoverIndices(3).Should().Equal(0, 1, 2);
            ClickServiceSeams.GetUltimatumPreHoverIndices(2).Should().Equal(0, 1);
            ClickServiceSeams.GetUltimatumPreHoverIndices(1).Should().Equal(0);
        }

        [TestMethod]
        public void GetUltimatumPreHoverIndices_ReturnsEmpty_WhenNoOptions()
        {
            ClickServiceSeams.GetUltimatumPreHoverIndices(0).Should().BeEmpty();
            ClickServiceSeams.GetUltimatumPreHoverIndices(-2).Should().BeEmpty();
        }

        [TestMethod]
        public void ShouldPrimeUltimatumGroundLabelTooltips_ReturnsTrue_WhenAnyModifierIsUnknown()
        {
            var priorities = new List<string> { "Resistant Monsters", "Choking Miasma", "Ruin" };
            var modifierNames = new List<string> { "Resistant Monsters", "Unknown Option 2", "Ruin" };

            bool shouldPrime = InvokeShouldPrimeUltimatumGroundLabelTooltips(modifierNames, priorities);

            shouldPrime.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPrimeUltimatumGroundLabelTooltips_ReturnsFalse_WhenAllModifiersAreKnown()
        {
            var priorities = new List<string> { "Resistant Monsters", "Choking Miasma", "Ruin" };
            var modifierNames = new List<string> { "Resistant Monsters", "Choking Miasma", "Ruin" };

            bool shouldPrime = InvokeShouldPrimeUltimatumGroundLabelTooltips(modifierNames, priorities);

            shouldPrime.Should().BeFalse();
        }

        [TestMethod]
        public void IsUltimatumPath_MatchesCaseInsensitiveUltimatumInteractablePath()
        {
            bool matched = InvokeIsUltimatumPath("metadata/terrain/leagues/ultimatum/objects/UltimatumChallengeInteractable");
            bool nonMatch = InvokeIsUltimatumPath("Metadata/Terrain/Leagues/Ritual/Objects/RitualRuneInteractable");

            matched.Should().BeTrue();
            nonMatch.Should().BeFalse();
        }

        private sealed class ThrowingGetterStub
        {
            public object Dangerous => throw new NullReferenceException("Simulated transient UI read failure");
        }
    }
}
