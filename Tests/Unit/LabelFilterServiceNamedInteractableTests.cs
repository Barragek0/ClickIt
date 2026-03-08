using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceNamedInteractableTests
    {
        [DataTestMethod]
        [DataRow(true, false, "door", true)]
        [DataRow(true, false, "Door", true)]
        [DataRow(true, false, "lever", false)]
        [DataRow(false, true, "lever", true)]
        [DataRow(false, true, "Lever", true)]
        [DataRow(false, true, "door", false)]
        [DataRow(false, false, "door", false)]
        [DataRow(true, true, "crate", false)]
        [DataRow(true, true, "", false)]
        [DataRow(true, true, null, false)]
        public void ShouldClickNamedInteractable_RespectsDoorAndLeverFlags(
            bool clickDoors,
            bool clickLevers,
            string? renderName,
            bool expected)
        {
            var method = typeof(Services.LabelFilterService).GetMethod(
                "ShouldClickNamedInteractable",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.Should().NotBeNull();

            bool result = (bool)method!.Invoke(null, new object?[] { clickDoors, clickLevers, renderName })!;
            result.Should().Be(expected);
        }
    }
}
