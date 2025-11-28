using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ClickIt.Services;
using ClickIt.Utils;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceSpecialTests
    {
        private static object? InvokePrivateStatic(string methodName, params object?[] args)
        {
            var method = typeof(LabelFilterService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return method!.Invoke(null, args);
        }

        private static object CreateClickSettingsWithFlag(string flagName, bool value)
        {
            var clickSettingsType = typeof(LabelFilterService).GetNestedType("ClickSettings", BindingFlags.NonPublic);
            clickSettingsType.Should().NotBeNull();
            var cs = System.Activator.CreateInstance(clickSettingsType!);
            var prop = clickSettingsType!.GetProperty(flagName, BindingFlags.Public | BindingFlags.Instance);
            prop.Should().NotBeNull();
            prop!.SetValue(cs, value);
            // ensure ClickDistance default is set so methods using distance don't fail
            var cdProp = clickSettingsType.GetProperty("ClickDistance", BindingFlags.Public | BindingFlags.Instance);
            cdProp!.SetValue(cs, 100);
            return cs!;
        }

        [TestMethod]
        public void IsHarvestPath_DetectsIrrigatorAndExtractor()
        {
            var m = typeof(LabelFilterService).GetMethod("IsHarvestPath", BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull();
            ((bool)m!.Invoke(null, new object[] { "Harvest/Irrigator" })).Should().BeTrue();
            ((bool)m!.Invoke(null, new object[] { "Harvest/Extractor" })).Should().BeTrue();
            ((bool)m!.Invoke(null, new object[] { "NotHarvest" })).Should().BeFalse();
        }

        [TestMethod]
        public void IsSettlersOrePath_DetectsKnownNames()
        {
            var m = typeof(LabelFilterService).GetMethod("IsSettlersOrePath", BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull();
            ((bool)m!.Invoke(null, new object[] { "CrimsonIron" })).Should().BeTrue();
            ((bool)m!.Invoke(null, new object[] { "copper_altar" })).Should().BeTrue();
            ((bool)m!.Invoke(null, new object[] { "Random/Path" })).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickSpecialPath_RespectsFlags_ForHarvestAndSulphite()
        {
            // Harvest
            var csHarvest = CreateClickSettingsWithFlag("NearestHarvest", true);
            ((bool)InvokePrivateStatic("ShouldClickSpecialPath", csHarvest, "Harvest/Irrigator", null)!).Should().BeTrue();

            // Sulphite (DelveMineral)
            var csSulphite = CreateClickSettingsWithFlag("ClickSulphite", true);
            ((bool)InvokePrivateStatic("ShouldClickSpecialPath", csSulphite, "DelveMineral/col1", null)!).Should().BeTrue();

            // ClickCrafting set -> should match CraftingUnlocks
            var csCraft = CreateClickSettingsWithFlag("ClickCrafting", true);
            ((bool)InvokePrivateStatic("ShouldClickSpecialPath", csCraft, "Some/Path/CraftingUnlocks", null)!).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickRitual_RespectsInitiateAndCompleted_BasicCases()
        {
            // deterministic test using the IElementAdapter test stub
            var noText = new TestUtils.ElementAdapterStub("");
            var hasFavours = new TestUtils.ElementAdapterStub("Interact to view Favours");

            // initial (no favours text) -> initiate should click, completed should not
            var init = LabelFilterService.ShouldClickRitualForTests(true, false, "Leagues/Ritual/abc", noText);
            init.Should().BeTrue();

            var completed = LabelFilterService.ShouldClickRitualForTests(false, true, "Leagues/Ritual/abc", noText);
            completed.Should().BeFalse();

            // when the label contains the favours text, completed should click
            var completedWhenFavours = LabelFilterService.ShouldClickRitualForTests(false, true, "Leagues/Ritual/abc", hasFavours);
            completedWhenFavours.Should().BeTrue();
        }

        // NOTE: intentionally omitted GetNextLabelToClick slice test due to runtime-dependent Entity/LabelOnGround fields that
        // require a running memory-backed GameController. Higher-level integration tests will exercise this in a safe environment.

        [TestMethod]
        public void GetNextLabelToClick_Slice_SearchesOnlyWindowAndRespectsDistance()
        {
            // Deterministic slice test implemented via a lightweight distances array (negative means no item)

            // distances: negative => no item; otherwise distance to player
            var distances = new int[] { -1, 200, 50, 30 };

            // invoke the private deterministic test seam added to LabelFilterService
            var helper = typeof(global::ClickIt.Services.LabelFilterService).GetMethod("GetNextLabelToClickIndexForTests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            helper.Should().NotBeNull();

            // scan [0,2) -> should skip indices 0..1 (no clickable) -> no clickable found
            var idx0 = (int?)helper!.Invoke(null, new object[] { distances, 0, 2, 100 });
            idx0.Should().BeNull();

            // scan [1,3) -> distances[1]=200 (too far), distances[2]=50 -> should return index 2
            var idx1 = (int?)helper!.Invoke(null, new object[] { distances, 1, 2, 100 });
            idx1.Should().Be(2);

            // scan [2,1) -> only check distances[2] -> should return index 2
            var idx2 = (int?)helper!.Invoke(null, new object[] { distances, 2, 1, 100 });
            idx2.Should().Be(2);

            // scan start beyond available -> return null
            var idx3 = (int?)helper!.Invoke(null, new object[] { distances, 10, 5, 100 });
            idx3.Should().BeNull();
        }

        [TestMethod]
        public void IsLabelObscuredByCloserLabelForTests_DetectsOverlappingCloserLabel()
        {
            // Candidate rect at index 0, center at (5,5)
            var rects = new SharpDX.RectangleF[] { new SharpDX.RectangleF(0,0,10,10), new SharpDX.RectangleF(0,0,6,6) };
            var distances = new int[] { 10, 5 }; // second label is closer

            var helper = typeof(global::ClickIt.Services.LabelFilterService).GetMethod("IsLabelObscuredByCloserLabelForTests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            helper.Should().NotBeNull();

            var res = (bool)helper!.Invoke(null, new object[] { rects, distances, 0 })!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void IsLabelObscuredByCloserLabelForTests_IgnoresFartherOverlappingLabel()
        {
            var rects = new SharpDX.RectangleF[] { new SharpDX.RectangleF(0,0,10,10), new SharpDX.RectangleF(0,0,6,6) };
            var distances = new int[] { 5, 20 }; // other is farther -> should not obscure

            var helper = typeof(global::ClickIt.Services.LabelFilterService).GetMethod("IsLabelObscuredByCloserLabelForTests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            helper.Should().NotBeNull();

            var res = (bool)helper!.Invoke(null, new object[] { rects, distances, 0 })!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void IsLabelObscuredByCloserLabelForTests_ReturnsFalse_WhenNoOverlap()
        {
            var rects = new SharpDX.RectangleF[] { new SharpDX.RectangleF(0,0,10,10), new SharpDX.RectangleF(20,20,5,5) };
            var distances = new int[] { 5, 2 };

            var helper = typeof(global::ClickIt.Services.LabelFilterService).GetMethod("IsLabelObscuredByCloserLabelForTests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            helper.Should().NotBeNull();

            var res = (bool)helper!.Invoke(null, new object[] { rects, distances, 0 })!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void Debug_DumpLabelOnGroundMembers()
        {
            var t = typeof(ExileCore.PoEMemory.Elements.LabelOnGround);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("LabelOnGround Properties:");
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                sb.AppendLine($" - {p.Name} ({p.PropertyType.FullName}) writable={p.CanWrite}");
            }
            sb.AppendLine("LabelOnGround Fields:");
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                sb.AppendLine($" - {f.Name} ({f.FieldType.FullName})");
            }
            var e = typeof(ExileCore.PoEMemory.MemoryObjects.Entity);
            sb.AppendLine("Entity Fields:");
            foreach (var f in e.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                sb.AppendLine($" - {f.Name} ({f.FieldType.FullName})");
            }
            var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clickit_labelonground_members.txt");
            System.IO.File.WriteAllText(tempFile, sb.ToString());
            System.Console.WriteLine($"Wrote debug dump to: {tempFile}");
            // keep test framework happy
            true.Should().BeTrue();
        }
    }
}
