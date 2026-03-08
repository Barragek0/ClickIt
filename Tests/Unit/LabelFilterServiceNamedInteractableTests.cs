using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceNamedInteractableTests
    {
        [DataTestMethod]
        [DataRow(true, false, "door", null, false)]
        [DataRow(true, false, "Door", null, false)]
        [DataRow(true, false, null, "Metadata/MiscellaneousObjects/Lights/IncaDoorLight", true)]
        [DataRow(true, false, null, "Metadata/MiscellaneousObjects/Lights/IncaDoorLightVariant01", true)]
        [DataRow(true, false, "Door", "Metadata/MiscellaneousObjects/Lights/IncaDoorLight", true)]
        [DataRow(true, false, "lever", null, false)]
        [DataRow(false, true, "lever", null, true)]
        [DataRow(false, true, "Lever", null, true)]
        [DataRow(false, true, "door", null, false)]
        [DataRow(false, false, "door", null, false)]
        [DataRow(true, true, "crate", "Metadata/MiscellaneousObjects/Containers/Crate", false)]
        [DataRow(true, true, "", null, false)]
        [DataRow(true, true, null, null, false)]
        public void ShouldClickNamedInteractable_RespectsDoorAndLeverFlags(
            bool clickDoors,
            bool clickLevers,
            string? renderName,
            string? metadataPath,
            bool expected)
        {
            var method = typeof(Services.LabelFilterService).GetMethod(
                "ShouldClickNamedInteractable",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.Should().NotBeNull();

            bool result = (bool)method!.Invoke(null, new object?[] { clickDoors, clickLevers, renderName, metadataPath })!;
            result.Should().Be(expected);
        }
    }
}
