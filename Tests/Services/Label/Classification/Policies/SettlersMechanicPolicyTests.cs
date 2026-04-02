using ClickIt.Definitions;
using ClickIt.Services.Label.Classification.Policies;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Label.Classification.Policies
{
    [TestClass]
    public class SettlersMechanicPolicyTests
    {
        [TestMethod]
        public void IsSettlersMechanicId_ReturnsTrue_OnlyForSettlersPrefix()
        {
            SettlersMechanicPolicy.IsSettlersMechanicId("settlers-crimson-iron").Should().BeTrue();
            SettlersMechanicPolicy.IsSettlersMechanicId("SeTtLeRs-copper").Should().BeTrue();
            SettlersMechanicPolicy.IsSettlersMechanicId("items").Should().BeFalse();
            SettlersMechanicPolicy.IsSettlersMechanicId(string.Empty).Should().BeFalse();
            SettlersMechanicPolicy.IsSettlersMechanicId(null).Should().BeFalse();
        }

        [TestMethod]
        public void RequiresHoldClick_ReturnsTrue_OnlyForVerisium()
        {
            SettlersMechanicPolicy.RequiresHoldClick(MechanicIds.SettlersVerisium).Should().BeTrue();
            SettlersMechanicPolicy.RequiresHoldClick(MechanicIds.SettlersCrimsonIron).Should().BeFalse();
            SettlersMechanicPolicy.RequiresHoldClick("items").Should().BeFalse();
            SettlersMechanicPolicy.RequiresHoldClick(null).Should().BeFalse();
        }
    }
}