using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using SharpDX;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceHelpersTests
    {
        [TestMethod]
        public void ShouldClickChestInternal_Behaves_Correctly_ForVariousNames()
        {
            var mi = typeof(LabelFilterService).GetMethod("ShouldClickChestInternal", BindingFlags.NonPublic | BindingFlags.Static)!;

            // Not a chest -> false
            var res1 = (bool)mi.Invoke(null, new object?[] { true, true, ExileCore.Shared.Enums.EntityType.Monster, "some/path", "chest" });
            res1.Should().BeFalse();

            // Basic chest by name -> true when clickBasicChests enabled
            var res2 = (bool)mi.Invoke(null, new object?[] { true, false, ExileCore.Shared.Enums.EntityType.Chest, null, "Chest" });
            res2.Should().BeTrue();

            // League chest (non-basic) -> true when clickLeagueChests enabled
            var res3 = (bool)mi.Invoke(null, new object?[] { false, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Some League" });
            res3.Should().BeTrue();

            // Strongbox path should be treated as not a basic chest
            var res4 = (bool)mi.Invoke(null, new object?[] { true, true, ExileCore.Shared.Enums.EntityType.Chest, "StrongBoxes/Strongbox", "strongbox" });
            res4.Should().BeFalse();
        }

        [TestMethod]
        public void PathHelpers_DetectHarvestAndSettlersOrePaths()
        {
            var isHarvest = typeof(LabelFilterService).GetMethod("IsHarvestPath", BindingFlags.NonPublic | BindingFlags.Static)!;
            var isSettlers = typeof(LabelFilterService).GetMethod("IsSettlersOrePath", BindingFlags.NonPublic | BindingFlags.Static)!;

            var rh1 = (bool)isHarvest.Invoke(null, new object?[] { "Some/Harvest/Irrigator/Path" });
            rh1.Should().BeTrue();
            var rh2 = (bool)isHarvest.Invoke(null, new object?[] { "Nothing/Here" });
            rh2.Should().BeFalse();

            var rs1 = (bool)isSettlers.Invoke(null, new object?[] { "PetrifiedWood/Some" });
            rs1.Should().BeTrue();
            var rs2 = (bool)isSettlers.Invoke(null, new object?[] { "Random/Path" });
            rs2.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_RequiresFlagAndPathPatterns()
        {
            var mi = typeof(LabelFilterService).GetMethod("ShouldClickAltar", BindingFlags.NonPublic | BindingFlags.Static)!;

            // No path -> false
            var ra1 = (bool)mi.Invoke(null, new object?[] { false, false, false, false, string.Empty });
            ra1.Should().BeFalse();

            // flags off even if path matches -> false
            var ra2 = (bool)mi.Invoke(null, new object?[] { false, false, false, false, "CleansingFireAltar" });
            ra2.Should().BeFalse();

            // flag on and path matches -> true
            var ra3 = (bool)mi.Invoke(null, new object?[] { true, false, false, false, "Some/CleansingFireAltar/Here" });
            ra3.Should().BeTrue();
            var ra4 = (bool)mi.Invoke(null, new object?[] { false, true, false, false, "This/TangleAltar" });
            ra4.Should().BeTrue();
        }

        [TestMethod]
        public void RectangleOverlap_Detects_OverlappingRects()
        {
            var mi = typeof(LabelFilterService).GetMethod("DoRectanglesOverlap", BindingFlags.NonPublic | BindingFlags.Static)!;

            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(5, 5, 10, 10); // overlap
            var c = new RectangleF(20, 20, 5, 5); // no overlap

            var overlapTrue = (bool)mi.Invoke(null, new object?[] { a, b });
            overlapTrue.Should().BeTrue();
            var overlapFalse = (bool)mi.Invoke(null, new object?[] { a, c });
            overlapFalse.Should().BeFalse();

            // Edges that just touch -> should be non-overlapping
            var leftTouch = new RectangleF(0, 0, 10, 10);
            var rightTouch = new RectangleF(10, 0, 5, 5);
            var overlapEdge = (bool)mi.Invoke(null, new object?[] { leftTouch, rightTouch });
            overlapEdge.Should().BeFalse();
        }

        [TestMethod]
        public void IsBasicChestName_AcceptsExpectedNames_IgnoresCase()
        {
            var mi = typeof(LabelFilterService).GetMethod("IsBasicChestName", BindingFlags.NonPublic | BindingFlags.Static)!;

            var cb1 = (bool)mi.Invoke(null, new object?[] { "chest" });
            cb1.Should().BeTrue();
            var cb2 = (bool)mi.Invoke(null, new object?[] { "Golden Chest" });
            cb2.Should().BeTrue();
            var cb3 = (bool)mi.Invoke(null, new object?[] { "weapon rack" });
            cb3.Should().BeTrue();
            var cb4 = (bool)mi.Invoke(null, new object?[] { "mystery" });
            cb4.Should().BeFalse();
            var cb5 = (bool)mi.Invoke(null, new object?[] { null });
            cb5.Should().BeFalse();
        }
    }
}
