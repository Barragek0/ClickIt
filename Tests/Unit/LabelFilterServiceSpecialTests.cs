using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ClickIt.Services;
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
            var init = (bool)InvokePrivateStatic("ShouldClickRitual", true, false, "Leagues/Ritual/abc", null)!;
            init.Should().BeTrue(); // initiate should click when no favours text present

            var completed = (bool)InvokePrivateStatic("ShouldClickRitual", false, true, "Leagues/Ritual/abc", null)!;
            completed.Should().BeFalse(); // without label text present we don't treat it as completed
        }

        [TestMethod]
        public void GetNextLabelToClick_SliceSearch_FindsNearestWithinSlice()
        {
            // Construct a few simple LabelOnGround objects
            var list = new List<LabelOnGround>();
            for (int i = 0; i < 5; i++)
            {
                var lbl = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
                var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
                // set distance and path
                var t = typeof(Entity);
                t.GetField("DistancePlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ent, 10 + i);
                t.GetField("Path", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ent, "some/Item" + i);
                // attach
                typeof(LabelOnGround).GetField("ItemOnGround", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(lbl, ent);
                list.Add(lbl);
            }

            var svc = new LabelFilterService(new ClickItSettings(), new Services.EssenceService(new ClickItSettings()), new ErrorHandler(new ClickItSettings(), (s, f) => { }, (s, f) => { }));

            // ensure ClickItems = true and click distance big enough
            var s = new ClickItSettings();
            s.ClickItems.Value = true;
            s.ClickDistance.Value = 20;

            // call GetNextLabelToClick with startIndex 2 and maxCount 2 -> should search labels 2 and 3 only
            var res = svc.GetNextLabelToClick(list, 2, 2);
            res.Should().NotBeNull();
            // the nearest one at slice start should be returned (distance 12)
            res!.ItemOnGround.DistancePlayer.Should().Be(12);
        }
    }
}
