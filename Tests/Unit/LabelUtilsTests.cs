using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsTests
    {
        [TestMethod]
        [Ignore("Duplicate of LabelUtils_PathTests; consolidated")]
        public void IsPathForClickableObject_ReturnsTrueForKnownPatterns()
        {
            string[] positives =
            [
                "Some/Path/DelveMineral",
                "Content/Delve/Objects/Encounter/123",
                "AzuriteEncounterController",
                "Harvest/Irrigator",
                "copper_altar",
                "Leagues/Ritual"
            ];

            foreach (var p in positives)
            {
                LabelUtils.IsPathForClickableObject(p).Should().BeTrue($"pattern '{p}' should match");
            }
        }

        [TestMethod]
        [Ignore("Duplicate of LabelUtils_PathTests; consolidated")]
        public void IsPathForClickableObject_ReturnsFalseForOtherPaths()
        {
            string[] negatives = ["Some/Other/Thing", "", "Random/Entity"];
            foreach (var p in negatives)
            {
                LabelUtils.IsPathForClickableObject(p).Should().BeFalse($"pattern '{p}' should not match");
            }
        }
    }
}
