using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using ClickIt.Services;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceChestInternalTests
    {
        private static object? InvokePrivateStatic(string methodName, params object?[] args)
        {
            var method = typeof(LabelFilterService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return method!.Invoke(null, args);
        }

        [TestMethod]
        public void ShouldClickChestInternal_ReturnsFalse_WhenTypeNotChest()
        {
            var res = (bool)InvokePrivateStatic("ShouldClickChestInternal", true, true, EntityType.WorldItem, "content/path", "chest")!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickChestInternal_ReturnsFalse_ForStrongboxPath()
        {
            // path contains strongbox -> treated as non-chest
            var res = (bool)InvokePrivateStatic("ShouldClickChestInternal", true, true, EntityType.Chest, "some/path/StrongBoxes/Arcanist", "Some Box")!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickChestInternal_ReturnsTrue_ForLeagueChest_WhenFlagEnabled_AndNotBasicName()
        {
            // non-basic name counts as a league chest if clickLeagueChests is enabled
            var res = (bool)InvokePrivateStatic("ShouldClickChestInternal", false, true, EntityType.Chest, "content/league/some", "Legendary Cache")!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickChestInternal_ReturnsFalse_WhenBothFlagsDisabled()
        {
            var res = (bool)InvokePrivateStatic("ShouldClickChestInternal", false, false, EntityType.Chest, "content/some", "Tribal Chest")!;
            res.Should().BeFalse();
        }
    }
}
