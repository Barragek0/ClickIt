using System.Reflection;
using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceLeverCooldownTests
    {
        private static bool InvokeSuppressed(ulong lastKey, long lastTs, ulong currentKey, long now, int cooldown)
        {
            var method = typeof(ClickService).GetMethod(
                "IsLeverClickSuppressedByCooldown",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.Should().NotBeNull();
            return (bool)method!.Invoke(null, new object[] { lastKey, lastTs, currentKey, now, cooldown })!;
        }

        [TestMethod]
        public void IsLeverClickSuppressedByCooldown_True_ForSameLeverWithinCooldown()
        {
            bool suppressed = InvokeSuppressed(100UL, 1_000L, 100UL, 1_500L, 600);
            suppressed.Should().BeTrue();
        }

        [TestMethod]
        public void IsLeverClickSuppressedByCooldown_False_ForDifferentLever()
        {
            bool suppressed = InvokeSuppressed(100UL, 1_000L, 200UL, 1_500L, 600);
            suppressed.Should().BeFalse();
        }

        [TestMethod]
        public void IsLeverClickSuppressedByCooldown_False_WhenCooldownElapsed()
        {
            bool suppressed = InvokeSuppressed(100UL, 1_000L, 100UL, 2_000L, 600);
            suppressed.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void IsLeverClickSuppressedByCooldown_False_WhenCooldownNotPositive(int cooldown)
        {
            bool suppressed = InvokeSuppressed(100UL, 1_000L, 100UL, 1_100L, cooldown);
            suppressed.Should().BeFalse();
        }

        [TestMethod]
        public void IsLeverClickSuppressedByCooldown_False_ForMissingIdentity()
        {
            InvokeSuppressed(0UL, 1_000L, 100UL, 1_100L, 500).Should().BeFalse();
            InvokeSuppressed(100UL, 1_000L, 0UL, 1_100L, 500).Should().BeFalse();
        }
    }
}
