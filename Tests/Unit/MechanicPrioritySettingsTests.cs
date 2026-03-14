using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class MechanicPrioritySettingsTests
    {
        [TestMethod]
        public void MechanicPriority_DefaultsContainAllKnownMechanics()
        {
            var settings = new ClickItSettings();

            var priorities = settings.GetMechanicPriorityOrder();
            var ignoreDistanceWithinById = settings.GetMechanicPriorityIgnoreDistanceWithinById();

            priorities.Should().Contain("basic-chests");
            priorities.Should().Contain("league-chests");
            priorities.Should().Contain("doors");
            priorities.Should().Contain("levers");
            priorities.Should().Contain("betrayal");
            priorities.Should().Contain("sanctum");
            priorities.Should().Contain("essences");
            priorities.Should().Contain("altars");
            priorities.Should().Contain("ultimatum");
            priorities.Should().Contain("shrines");
            priorities.Should().Contain("lost-shipment");
            priorities.Should().Contain("strongboxes");
            priorities.Should().Contain("items");
            var priorityList = priorities.ToList();
            priorityList.IndexOf("shrines").Should().BeLessThan(priorityList.IndexOf("lost-shipment"));
            priorityList.IndexOf("lost-shipment").Should().BeLessThan(priorityList.IndexOf("items"));
            priorities.Count.Should().Be(priorities.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            ignoreDistanceWithinById.Keys.Should().Contain(priorities);
            ignoreDistanceWithinById.Values.Should().OnlyContain(v => v == 100);
        }

        [TestMethod]
        public void MechanicPriority_SanitizesUnknownAndDuplicateEntries()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityOrder = new List<string>
                {
                    "essences",
                    "unknown-mechanic",
                    "essences"
                },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "essences",
                    "unknown-mechanic"
                },
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["essences"] = 999,
                    ["unknown-mechanic"] = 50
                }
            };

            var priorities = settings.GetMechanicPriorityOrder();
            var ignoreDistance = settings.GetMechanicPriorityIgnoreDistanceIds();
            var ignoreDistanceWithinById = settings.GetMechanicPriorityIgnoreDistanceWithinById();

            priorities.Should().Contain("essences");
            priorities.Should().NotContain("unknown-mechanic");
            priorities.Count.Should().Be(priorities.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            ignoreDistance.Should().Contain("essences");
            ignoreDistance.Should().NotContain("unknown-mechanic");
            ignoreDistanceWithinById.Should().ContainKey("essences");
            ignoreDistanceWithinById["essences"].Should().Be(500);
            ignoreDistanceWithinById.Should().NotContainKey("unknown-mechanic");
        }

        [TestMethod]
        public void MechanicPriority_PersistsOrderAndIgnoreDistanceAcrossJsonRoundTrip()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityOrder = new List<string>
                {
                    "essences",
                    "ultimatum",
                    "items"
                },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(new[] { "essences", "ultimatum" }, StringComparer.OrdinalIgnoreCase),
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["essences"] = 75,
                    ["ultimatum"] = 125
                }
            };

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();

            var priorities = restored!.GetMechanicPriorityOrder();
            var ignoreDistance = restored.GetMechanicPriorityIgnoreDistanceIds();
            var ignoreDistanceWithinById = restored.GetMechanicPriorityIgnoreDistanceWithinById();

            priorities[0].Should().Be("essences");
            priorities[1].Should().Be("ultimatum");
            priorities[2].Should().Be("items");
            ignoreDistance.Should().Contain("essences");
            ignoreDistance.Should().Contain("ultimatum");
            ignoreDistanceWithinById["essences"].Should().Be(75);
            ignoreDistanceWithinById["ultimatum"].Should().Be(125);
        }

        [TestMethod]
        public void MechanicPriority_MigratesLegacyJsonWithoutOverwritingPrioritySettings()
        {
            const string legacyJson = "{\"MechanicPriorityOrder\":[\"essences\",\"items\",\"shrines\"],\"MechanicPriorityIgnoreDistanceIds\":[\"essences\"],\"MechanicPriorityDistancePenalty\":{\"Value\":25}}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();

            var priorities = restored!.GetMechanicPriorityOrder();
            var ignoreDistance = restored.GetMechanicPriorityIgnoreDistanceIds();
            var ignoreDistanceWithinById = restored.GetMechanicPriorityIgnoreDistanceWithinById();

            priorities[0].Should().Be("essences");
            priorities[1].Should().Be("items");
            priorities[2].Should().Be("shrines");
            ignoreDistance.Should().ContainSingle().Which.Should().Be("essences");
            ignoreDistanceWithinById["essences"].Should().Be(100);
            ignoreDistanceWithinById["items"].Should().Be(100);
        }
    }
}