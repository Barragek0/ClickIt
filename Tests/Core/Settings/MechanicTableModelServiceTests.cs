using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class MechanicTableModelServiceTests
    {
        [TestMethod]
        public void GetTableEntries_BuildsCachedEntryListFromSettingsNodes()
        {
            var settings = new global::ClickIt.ClickItSettings();
            settings.ClickRitualCompleted.Value = false;

            var entries = global::ClickIt.MechanicTableModelService.GetTableEntries(settings);
            var ritualCompleted = entries.Should().ContainSingle(entry => entry.Id == global::ClickIt.Definitions.MechanicIds.RitualCompleted).Subject;

            ritualCompleted.Node.Should().BeSameAs(settings.ClickRitualCompleted);
            ritualCompleted.DefaultEnabled.Should().BeTrue();
        }
    }
}