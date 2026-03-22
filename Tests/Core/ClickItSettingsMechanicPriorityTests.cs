using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            priorities.Should().Contain("altars-searing-exarch");
            priorities.Should().Contain("altars-eater-of-worlds");
            priorities.Should().Contain("ultimatum-initial-overlay");
            priorities.Should().Contain("ultimatum-window");
            priorities.Should().Contain("shrines");
            priorities.Should().Contain("lost-shipment");
            priorities.Should().Contain("strongboxes");
            priorities.Should().Contain("items");
            priorities.Should().Contain("delve-sulphite-veins");
            priorities.Should().Contain("delve-azurite-veins");
            priorities.Should().Contain("delve-encounter-initiators");
            priorities.Should().Contain("settlers-crimson-iron");
            priorities.Should().Contain("settlers-copper");
            priorities.Should().Contain("settlers-petrified-wood");
            priorities.Should().Contain("settlers-bismuth");
            priorities.Should().Contain("settlers-hourglass");
            priorities.Should().Contain("settlers-verisium");
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
            priorities.Should().ContainInOrder("essences", "ultimatum-initial-overlay", "ultimatum-window", "items");
            ignoreDistance.Should().Contain("essences");
            ignoreDistance.Should().Contain("ultimatum-initial-overlay");
            ignoreDistance.Should().Contain("ultimatum-window");
            ignoreDistanceWithinById["essences"].Should().Be(75);
            ignoreDistanceWithinById["ultimatum-initial-overlay"].Should().Be(125);
            ignoreDistanceWithinById["ultimatum-window"].Should().Be(125);
        }

        [TestMethod]
        public void MechanicPriority_MigratesLegacyJsonWithoutOverwritingPrioritySettings()
        {
            const string legacyJson = "{\"MechanicPriorityOrder\":[\"altars\",\"ultimatum\",\"essences\",\"items\",\"shrines\"],\"MechanicPriorityIgnoreDistanceIds\":[\"ultimatum\"],\"MechanicPriorityDistancePenalty\":{\"Value\":25}}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();

            var priorities = restored!.GetMechanicPriorityOrder();
            var ignoreDistance = restored.GetMechanicPriorityIgnoreDistanceIds();
            var ignoreDistanceWithinById = restored.GetMechanicPriorityIgnoreDistanceWithinById();

            priorities.Should().ContainInOrder(
                "altars-searing-exarch",
                "altars-eater-of-worlds",
                "ultimatum-initial-overlay",
                "ultimatum-window",
                "essences",
                "items",
                "shrines");
            ignoreDistance.Should().Contain("ultimatum-initial-overlay");
            ignoreDistance.Should().Contain("ultimatum-window");
            ignoreDistanceWithinById["ultimatum-initial-overlay"].Should().Be(100);
            ignoreDistanceWithinById["ultimatum-window"].Should().Be(100);
            ignoreDistanceWithinById["items"].Should().Be(100);
        }

        [TestMethod]
        public void MechanicPriority_MigratesLegacySettlersOreIds_ToSplitSettlersIds()
        {
            const string legacyJson = "{\"MechanicPriorityOrder\":[\"settlers-ore\",\"items\"],\"MechanicPriorityIgnoreDistanceIds\":[\"settlers-ore\"],\"MechanicPriorityIgnoreDistanceWithinById\":{\"settlers-ore\":90}}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();

            var priorities = restored!.GetMechanicPriorityOrder();
            var ignoreDistance = restored.GetMechanicPriorityIgnoreDistanceIds();
            var ignoreDistanceWithinById = restored.GetMechanicPriorityIgnoreDistanceWithinById();

            priorities.Should().ContainInOrder(
                "settlers-crimson-iron",
                "settlers-copper",
                "settlers-petrified-wood",
                "settlers-bismuth",
                "settlers-hourglass",
                "settlers-verisium",
                "items");

            ignoreDistance.Should().Contain("settlers-crimson-iron");
            ignoreDistance.Should().Contain("settlers-copper");
            ignoreDistance.Should().Contain("settlers-petrified-wood");
            ignoreDistance.Should().Contain("settlers-bismuth");
            ignoreDistance.Should().Contain("settlers-hourglass");
            ignoreDistance.Should().Contain("settlers-verisium");

            ignoreDistanceWithinById["settlers-crimson-iron"].Should().Be(90);
            ignoreDistanceWithinById["settlers-copper"].Should().Be(90);
            ignoreDistanceWithinById["settlers-petrified-wood"].Should().Be(90);
            ignoreDistanceWithinById["settlers-bismuth"].Should().Be(90);
            ignoreDistanceWithinById["settlers-hourglass"].Should().Be(90);
            ignoreDistanceWithinById["settlers-verisium"].Should().Be(90);
        }

        [TestMethod]
        public void MechanicsSubmenuLogic_LeftAndRightColumnRenderingFollowNodeValue()
        {
            var settings = new ClickItSettings();

            MethodInfo? getEntriesMethod = typeof(ClickItSettings).GetMethod("GetMechanicTableEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo? shouldRenderMethod = typeof(ClickItSettings).GetMethod("ShouldRenderMechanicEntry", BindingFlags.Static | BindingFlags.NonPublic);
            getEntriesMethod.Should().NotBeNull();
            shouldRenderMethod.Should().NotBeNull();

            var entries = ((System.Collections.IEnumerable)getEntriesMethod!.Invoke(settings, null)!).Cast<object>().ToList();
            object ritualCompletedEntry = entries.First(entry =>
                string.Equals((string)entry.GetType().GetProperty("Id")!.GetValue(entry)!, "ritual-completed", StringComparison.Ordinal));

            settings.ClickRitualCompleted.Value = true;
            ((bool)shouldRenderMethod!.Invoke(null, [ritualCompletedEntry, false, string.Empty])!).Should().BeTrue();
            ((bool)shouldRenderMethod.Invoke(null, [ritualCompletedEntry, true, string.Empty])!).Should().BeFalse();

            settings.ClickRitualCompleted.Value = false;
            ((bool)shouldRenderMethod.Invoke(null, [ritualCompletedEntry, false, string.Empty])!).Should().BeFalse();
            ((bool)shouldRenderMethod.Invoke(null, [ritualCompletedEntry, true, string.Empty])!).Should().BeTrue();
        }

        [TestMethod]
        public void MechanicsSubmenuLogic_GroupToggleUpdatesUnderlyingMechanicStates()
        {
            var settings = new ClickItSettings();
            settings.ClickRitualInitiate.Value = true;
            settings.ClickRitualCompleted.Value = true;

            MethodInfo? getEntriesMethod = typeof(ClickItSettings).GetMethod("GetMechanicTableEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo? setGroupStateMethod = typeof(ClickItSettings).GetMethod("SetMechanicGroupState", BindingFlags.Static | BindingFlags.NonPublic);
            getEntriesMethod.Should().NotBeNull();
            setGroupStateMethod.Should().NotBeNull();

            object entries = getEntriesMethod!.Invoke(settings, null)!;
            setGroupStateMethod!.Invoke(null, ["ritual-altars", entries, false]);

            settings.ClickRitualInitiate.Value.Should().BeFalse();
            settings.ClickRitualCompleted.Value.Should().BeFalse();

            setGroupStateMethod.Invoke(null, ["ritual-altars", entries, true]);

            settings.ClickRitualInitiate.Value.Should().BeTrue();
            settings.ClickRitualCompleted.Value.Should().BeTrue();
        }

        [TestMethod]
        public void MechanicsSubmenuLogic_BasicAndLeagueChestRowsAreGroupedForSubmenus()
        {
            var settings = new ClickItSettings();

            MethodInfo? getEntriesMethod = typeof(ClickItSettings).GetMethod("GetMechanicTableEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            getEntriesMethod.Should().NotBeNull();

            var entries = ((System.Collections.IEnumerable)getEntriesMethod!.Invoke(settings, null)!).Cast<object>().ToList();
            object basicChestEntry = entries.First(entry =>
                string.Equals((string)entry.GetType().GetProperty("Id")!.GetValue(entry)!, "basic-chests", StringComparison.Ordinal));
            object leagueChestEntry = entries.First(entry =>
                string.Equals((string)entry.GetType().GetProperty("Id")!.GetValue(entry)!, "league-chests", StringComparison.Ordinal));

            string? basicGroupId = (string?)basicChestEntry.GetType().GetProperty("GroupId")!.GetValue(basicChestEntry);
            string? leagueGroupId = (string?)leagueChestEntry.GetType().GetProperty("GroupId")!.GetValue(leagueChestEntry);

            basicGroupId.Should().Be("basic-chests");
            leagueGroupId.Should().Be("league-chests");
        }

        [TestMethod]
        public void ChestLootSettlementSettings_DefaultToEnabled()
        {
            var settings = new ClickItSettings();

            settings.PauseAfterOpeningBasicChests.Value.Should().BeTrue();
            settings.PauseAfterOpeningBasicChestsInitialDelayMs.Value.Should().Be(500);
            settings.PauseAfterOpeningBasicChestsPollIntervalMs.Value.Should().Be(100);
            settings.PauseAfterOpeningBasicChestsQuietWindowMs.Value.Should().Be(500);
            settings.PauseAfterOpeningLeagueChests.Value.Should().BeTrue();
            settings.PauseAfterOpeningLeagueChestsInitialDelayMs.Value.Should().Be(500);
            settings.PauseAfterOpeningLeagueChestsPollIntervalMs.Value.Should().Be(100);
            settings.PauseAfterOpeningLeagueChestsQuietWindowMs.Value.Should().Be(500);
        }

        [TestMethod]
        public void MechanicsSubmenuLogic_LeagueChestSubmenuContainsSecureLockerHeistEntry()
        {
            var settings = new ClickItSettings();

            MethodInfo? getEntriesMethod = typeof(ClickItSettings).GetMethod("GetMechanicTableEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            getEntriesMethod.Should().NotBeNull();

            var entries = ((System.Collections.IEnumerable)getEntriesMethod!.Invoke(settings, null)!).Cast<object>().ToList();
            object secureLockerEntry = entries.First(entry =>
                string.Equals((string)entry.GetType().GetProperty("Id")!.GetValue(entry)!, "heist-secure-locker", StringComparison.Ordinal));

            string? groupId = (string?)secureLockerEntry.GetType().GetProperty("GroupId")!.GetValue(secureLockerEntry);
            string? subgroup = (string?)secureLockerEntry.GetType().GetProperty("Subgroup")!.GetValue(secureLockerEntry);

            groupId.Should().Be("league-chests");
            subgroup.Should().Be("Heist");
        }

        [TestMethod]
        public void MechanicsSubmenuLogic_LeagueChestSubmenuContainsGraspingCoffersBreachEntry()
        {
            var settings = new ClickItSettings();

            MethodInfo? getEntriesMethod = typeof(ClickItSettings).GetMethod("GetMechanicTableEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            getEntriesMethod.Should().NotBeNull();

            var entries = ((System.Collections.IEnumerable)getEntriesMethod!.Invoke(settings, null)!).Cast<object>().ToList();
            object graspingCoffersEntry = entries.First(entry =>
                string.Equals((string)entry.GetType().GetProperty("Id")!.GetValue(entry)!, "breach-grasping-coffers", StringComparison.Ordinal));

            string? groupId = (string?)graspingCoffersEntry.GetType().GetProperty("GroupId")!.GetValue(graspingCoffersEntry);
            string? subgroup = (string?)graspingCoffersEntry.GetType().GetProperty("Subgroup")!.GetValue(graspingCoffersEntry);

            groupId.Should().Be("league-chests");
            subgroup.Should().Be("Breach");
        }
    }
}