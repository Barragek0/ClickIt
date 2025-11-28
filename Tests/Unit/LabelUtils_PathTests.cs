using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtils_PathTests
    {
        [TestMethod]
        public void IsPathForClickableObject_MatchesKnownClickablePaths()
        {
            var positives = new[]
            {
                "DelveMineral/col1",
                "Some/Delve/Objects/Encounter/foo",
                "Harvest/Irrigator/xyz",
                "Harvest/Extractor/abc",
                "CleansingFireAltar/whatever",
                "TangleAltar/something",
                "Leagues/Ritual/blah",
                "ClosedDoorPast/door"
            };

            foreach (var p in positives)
            {
                LabelUtils.IsPathForClickableObject(p).Should().BeTrue($"path '{p}' should be considered clickable");
            }
        }

        [TestMethod]
        public void IsPathForClickableObject_ReturnsFalse_ForUnrelatedPaths()
        {
            var negatives = new[]
            {
                "Player/Character/John",
                "Monsters/BossArea",
                "some/random/path",
                string.Empty
            };

            foreach (var p in negatives)
            {
                LabelUtils.IsPathForClickableObject(p).Should().BeFalse($"path '{p ?? "<null>"}' should NOT be considered clickable");
            }
        }
    }
}
