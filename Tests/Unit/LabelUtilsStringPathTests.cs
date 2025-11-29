using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using ClickIt.Utils;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsStringPathTests
    {
        [TestMethod]
        public void IsPathForClickableObject_ReturnsTrueForKnownPatterns()
        {
            string[] patterns = new[] {
                "DelveMineral",
                "Delve/Objects/Encounter",
                "AzuriteEncounterController",
                "Harvest/Irrigator",
                "Harvest/Extractor",
                "CleansingFireAltar",
                "TangleAltar",
                "CraftingUnlocks",
                "Brequel",
                "CrimsonIron",
                "copper_altar",
                "PetrifiedWood",
                "Bismuth",
                "ClosedDoorPast",
                "LegionInitiator",
                "DarkShrine",
                "Sanctum",
                "BetrayalMakeChoice",
                "BlightPump",
                "Leagues/Ritual"
            };

            foreach (var p in patterns)
            {
                LabelUtils.IsPathForClickableObject(p).Should().BeTrue($"pattern '{p}' should be recognized as clickable");
            }
        }

        [TestMethod]
        public void IsPathForClickableObject_ReturnsFalseForUnrelatedPaths()
        {
            LabelUtils.IsPathForClickableObject("some/random/path/without/matching/keywords").Should().BeFalse();
            LabelUtils.IsPathForClickableObject(string.Empty).Should().BeFalse();
        }

        // NOTE: we avoid constructing or mutating runtime-backed LabelOnGround/Element
        // instances in unit tests because their accessors perform native memory reads
        // that will crash in the test runner. Higher-level adapter/seam tests cover
        // element-driven behaviors instead.
    }
}
