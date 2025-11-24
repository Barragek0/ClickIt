using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;

namespace ClickIt.Tests.Unit.Utils
{
    [TestClass]
    public class LabelUtilsTests
    {
        [TestMethod]
        public void IsPathForClickableObject_ReturnsTrue_ForKnownPatterns()
        {
            // direct call is safe for pure string checks (no ExileCore types required)

            var trues = new[]
            {
                "Some/DelveMineral/Path",
                "This/contains/Harvest/Extractor",
                "CleansingFireAltar",
                "LegionInitiator/path",
                "Leagues/Ritual"
            };

            foreach (var s in trues)
            {
                var result = ClickIt.Utils.LabelUtils.IsPathForClickableObject(s);
                result.Should().BeTrue($"'{s}' should match a clickable path pattern");
            }
        }

        [TestMethod]
        public void IsPathForClickableObject_ReturnsFalse_ForIrrelevantPath()
        {
            var result = ClickIt.Utils.LabelUtils.IsPathForClickableObject("Some/irrelevant/path");
            result.Should().BeFalse();
        }

        [TestMethod]
        public void Sort_and_swap_partition_operations_work_as_expected()
        {
            // build a small list of LabelOnGround with varying distances
            var a = new ExileCore.PoEMemory.Elements.LabelOnGround { ItemOnGround = new ExileCore.PoEMemory.MemoryObjects.Entity { DistancePlayer = 10 } };
            var b = new ExileCore.PoEMemory.Elements.LabelOnGround { ItemOnGround = new ExileCore.PoEMemory.MemoryObjects.Entity { DistancePlayer = 30 } };
            var c = new ExileCore.PoEMemory.Elements.LabelOnGround { ItemOnGround = new ExileCore.PoEMemory.MemoryObjects.Entity { DistancePlayer = 20 } };

            var list = new System.Collections.Generic.List<ExileCore.PoEMemory.Elements.LabelOnGround> { a, b, c };

            // Insertion sort
            ClickIt.Utils.LabelUtils.InsertionSortByDistance(list, list.Count);
            list[0].ItemOnGround.DistancePlayer.Should().Be(10);
            list[1].ItemOnGround.DistancePlayer.Should().Be(20);
            list[2].ItemOnGround.DistancePlayer.Should().Be(30);

            // swap
            ClickIt.Utils.LabelUtils.SwapLabels(list, 0, 2);
            list[0].ItemOnGround.DistancePlayer.Should().Be(30);

            // partition/quick sort
            var list2 = new System.Collections.Generic.List<ExileCore.PoEMemory.Elements.LabelOnGround> { a, b, c };
            int pivot = ClickIt.Utils.LabelUtils.PartitionByDistance(list2, 0, list2.Count - 1);
            pivot.Should().BeInRange(0, list2.Count - 1);

            ClickIt.Utils.LabelUtils.QuickSortByDistance(list2, 0, list2.Count - 1);
            list2[0].ItemOnGround.DistancePlayer.Should().BeLessOrEqualTo(list2[1].ItemOnGround.DistancePlayer);

            // sorting dispatcher
            var list3 = new System.Collections.Generic.List<ExileCore.PoEMemory.Elements.LabelOnGround>();
            ClickIt.Utils.LabelUtils.SortLabelsByDistance(list3); // empty -> no-op

            // small list -> insertion sort path
            ClickIt.Utils.LabelUtils.SortLabelsByDistance(new System.Collections.Generic.List<ExileCore.PoEMemory.Elements.LabelOnGround> { a, b });
        }

        [TestMethod]
        public void Label_element_helpers_and_string_traversal_work()
        {
            // IsLabelElementValid - create a label element with text and client rect
            var rootElement = new ExileCore.PoEMemory.Element { Text = "root text" };
            var labelOnGround = new ExileCore.PoEMemory.Elements.LabelOnGround { Label = rootElement, ItemOnGround = new ExileCore.PoEMemory.MemoryObjects.Entity() };

            bool areaCheck(SharpDX.Vector2 v) => true;

            ClickIt.Utils.LabelUtils.IsLabelElementValid(labelOnGround, areaCheck).Should().BeTrue();
            ClickIt.Utils.LabelUtils.IsLabelInClickableArea(labelOnGround, areaCheck).Should().BeTrue();

            // HasEssenceImprisonmentText - nested child contains the special phrase
            var child = new ExileCore.PoEMemory.Element { Text = "The monster is imprisoned by powerful Essences." };
            rootElement.Children = new System.Collections.Generic.List<ExileCore.PoEMemory.Element> { child };

            ClickIt.Utils.LabelUtils.HasEssenceImprisonmentText(labelOnGround).Should().BeTrue();

            // GetElementsByStringContains - should find root and child when they contain the substring
            rootElement.Text = "contains needle";
            var found = ClickIt.Utils.LabelUtils.GetElementsByStringContains(rootElement, "needle");
            found.Should().NotBeNull();
            found.Count.Should().BeGreaterOrEqualTo(1);

            // GetElementByString - exact match
            var exactRoot = new ExileCore.PoEMemory.Element { Text = "first" };
            var nested = new ExileCore.PoEMemory.Element { Text = "target" };
            exactRoot.Children = new System.Collections.Generic.List<ExileCore.PoEMemory.Element> { nested };
            var getExact = ClickIt.Utils.LabelUtils.GetElementByString(exactRoot, "target");
            getExact.Should().NotBeNull();
            getExact!.GetText(0).Should().Be("target");

            // ElementContainsAnyStrings
            var probe = ClickIt.Utils.LabelUtils.ElementContainsAnyStrings(exactRoot, new[] { "none", "tar" });
            probe.Should().BeTrue();
        }
    }
}
