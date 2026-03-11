using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceHelpersTests
    {
        [TestMethod]
        public void GetChestMechanicIdInternal_Behaves_Correctly_ForVariousNames()
        {
            var mi = typeof(LabelFilterService).GetMethod("GetChestMechanicIdInternal", BindingFlags.NonPublic | BindingFlags.Static)!;

            // Not a chest -> null
            var res1 = (string?)mi.Invoke(null, new object?[] { true, true, ExileCore.Shared.Enums.EntityType.Monster, "some/path", "chest" });
            res1.Should().BeNull();

            // Basic chest by name -> basic-chests when clickBasicChests enabled
            var res2 = (string?)mi.Invoke(null, new object?[] { true, false, ExileCore.Shared.Enums.EntityType.Chest, null, "Chest" });
            res2.Should().Be("basic-chests");

            // League chest (non-basic) -> league-chests when clickLeagueChests enabled
            var res3 = (string?)mi.Invoke(null, new object?[] { false, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Some League" });
            res3.Should().Be("league-chests");

            // Strongbox path should be excluded from generic chest mechanics
            var res4 = (string?)mi.Invoke(null, new object?[] { true, true, ExileCore.Shared.Enums.EntityType.Chest, "StrongBoxes/Strongbox", "strongbox" });
            res4.Should().BeNull();
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
