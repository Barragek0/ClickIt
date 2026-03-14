using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceHelpersTests
    {
        private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
        {
            var method = typeof(LabelFilterService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            var value = method!.Invoke(null, args);
            if (value == null)
                return default!;

            value.Should().BeAssignableTo<T>();
            return (T)value;
        }

        [TestMethod]
        public void GetChestMechanicIdInternal_Behaves_Correctly_ForVariousNames()
        {
            var res1 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", true, true, ExileCore.Shared.Enums.EntityType.Monster, "some/path", "chest");
            res1.Should().BeNull();

            var res2 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", true, false, ExileCore.Shared.Enums.EntityType.Chest, null, "Chest");
            res2.Should().Be("basic-chests");

            var res3 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Some League");
            res3.Should().Be("league-chests");

            var res4 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", true, true, ExileCore.Shared.Enums.EntityType.Chest, "StrongBoxes/Strongbox", "strongbox");
            res4.Should().BeNull();
        }

        [TestMethod]
        public void PathHelpers_DetectHarvestAndSettlersOrePaths()
        {
            var rh1 = InvokePrivateStatic<bool>("IsHarvestPath", "Some/Harvest/Irrigator/Path");
            rh1.Should().BeTrue();
            var rh2 = InvokePrivateStatic<bool>("IsHarvestPath", "Nothing/Here");
            rh2.Should().BeFalse();

            var rs1 = InvokePrivateStatic<bool>("IsSettlersOrePath", "PetrifiedWood/Some");
            rs1.Should().BeTrue();
            var rs2 = InvokePrivateStatic<bool>("IsSettlersOrePath", "Random/Path");
            rs2.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickAltar_RequiresFlagAndPathPatterns()
        {
            var ra1 = InvokePrivateStatic<bool>("ShouldClickAltar", false, false, false, false, string.Empty);
            ra1.Should().BeFalse();

            var ra2 = InvokePrivateStatic<bool>("ShouldClickAltar", false, false, false, false, "CleansingFireAltar");
            ra2.Should().BeFalse();

            var ra3 = InvokePrivateStatic<bool>("ShouldClickAltar", true, false, false, false, "Some/CleansingFireAltar/Here");
            ra3.Should().BeTrue();
            var ra4 = InvokePrivateStatic<bool>("ShouldClickAltar", false, true, false, false, "This/TangleAltar");
            ra4.Should().BeTrue();
        }

        [TestMethod]
        public void IsBasicChestName_AcceptsExpectedNames_IgnoresCase()
        {
            var cb1 = InvokePrivateStatic<bool>("IsBasicChestName", "chest");
            cb1.Should().BeTrue();
            var cb2 = InvokePrivateStatic<bool>("IsBasicChestName", "Golden Chest");
            cb2.Should().BeTrue();
            var cb3 = InvokePrivateStatic<bool>("IsBasicChestName", "weapon rack");
            cb3.Should().BeTrue();
            var cb4 = InvokePrivateStatic<bool>("IsBasicChestName", "mystery");
            cb4.Should().BeFalse();
            var cb5 = InvokePrivateStatic<bool>("IsBasicChestName", (string?)null);
            cb5.Should().BeFalse();
        }
    }
}
