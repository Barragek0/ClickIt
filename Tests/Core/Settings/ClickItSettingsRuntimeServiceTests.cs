namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsRuntimeServiceTests
    {
        [TestMethod]
        public void GetMechanicPriorityOrder_RebuildsSnapshotWhenSelectionChanges()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityOrder = new List<string> { "essences", "items" },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItSettings.PriorityComparer),
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
            };

            IReadOnlyList<string> initial = ClickItSettingsRuntimeService.GetMechanicPriorityOrder(settings);
            settings.MechanicPriorityOrder = new List<string> { "items", "essences" };

            IReadOnlyList<string> updated = ClickItSettingsRuntimeService.GetMechanicPriorityOrder(settings);

            updated.Should().ContainInOrder("items", "essences");
            updated.Should().NotEqual(initial);
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceIds_ReusesSnapshot_WhenSetUnchanged()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItSettings.PriorityComparer) { "shrines", "essences" },
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
            };

            IReadOnlyCollection<string> first = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);
            IReadOnlyCollection<string> second = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceIds_Rebuilds_WhenSameCountSetChangesContent()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItSettings.PriorityComparer) { "shrines", "essences" },
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
            };

            IReadOnlyCollection<string> initial = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);

            settings.MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItSettings.PriorityComparer) { "shrines", "strongboxes" };

            IReadOnlyCollection<string> updated = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);

            updated.Should().NotBeSameAs(initial);
            updated.Should().Equal("shrines", "strongboxes");
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceWithinById_ReusesSnapshot_WhenMapUnchanged()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
                {
                    ["shrines"] = 40,
                    ["essences"] = 60,
                }
            };

            IReadOnlyDictionary<string, int> first = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);
            IReadOnlyDictionary<string, int> second = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceWithinById_Rebuilds_WhenValueChanges()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
                {
                    ["shrines"] = 40,
                }
            };

            ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);

            settings.MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
            {
                ["shrines"] = 80,
            };

            IReadOnlyDictionary<string, int> updated = ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);

            updated["shrines"].Should().Be(80);
        }
    }
}