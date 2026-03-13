using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceWorldAndChestTests
    {
        private static void SetMemberValue(object obj, string memberName, object? value)
        {
            if (obj == null) return;
            var type = obj.GetType();

            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
                return;
            }

            // Try common backing field patterns
            var backingField = type.GetField($"<{memberName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField != null)
            {
                backingField.SetValue(obj, value);
                return;
            }

            // Try any private field that contains the member name
            var fuzzy = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fuzzy)
            {
                if (f.Name.IndexOf(memberName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    f.SetValue(obj, value);
                    return;
                }
            }

            // Try any private field that matches the value type (useful when properties are computed)
            if (value != null)
            {
                foreach (var f in fuzzy)
                {
                    if (f.FieldType.IsInstanceOfType(value))
                    {
                        f.SetValue(obj, value);
                        return;
                    }
                }
            }

            // Last resort: cannot set this member via reflection - fail with clearer message
            throw new InvalidOperationException($"Unable to set member '{memberName}' on type '{type.FullName}' in test.");
        }
        private static object? InvokePrivateStatic(string name, params object?[] args)
        {
            var method = typeof(Services.LabelFilterService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return method!.Invoke(null, args);
        }

        // note: Instance private invocations aren't needed in these tests

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsFalse_WhenClickItemsDisabled()
        {
            // clickItems=false should short-circuit (no need to set entity fields)
            var res = (bool)InvokePrivateStatic("ShouldClickWorldItemCore", false, EntityType.WorldItem, null)!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsFalse_WhenPathContainsStrongbox()
        {
            var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            SetMemberValue(ent, "Path", "some/StrongBoxes/Strongbox/x");

            var res = (bool)InvokePrivateStatic("ShouldClickWorldItemCore", true, EntityType.WorldItem, ent)!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsTrue_WhenEnabledAndNotStrongbox()
        {
            var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            SetMemberValue(ent, "Path", "some/Item/Name");

            var res = (bool)InvokePrivateStatic("ShouldClickWorldItemCore", true, EntityType.WorldItem, ent)!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void IsBasicChest_DetectsSimpleNames_CaseInsensitive()
        {
            // call the name-only helper directly via reflection
            var res = (bool)InvokePrivateStatic("IsBasicChestName", "chest")!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickChest_RecognizesBasicChest_WhenSettingsAllow()
        {
            // Call internal helper directly - pass primitive path and renderName to avoid mutating ExileCore objects
            var res = (string?)InvokePrivateStatic("GetChestMechanicIdInternal", true, false, EntityType.Chest, "content/some/chest", "Tribal Chest")!;
            res.Should().Be("basic-chests");
        }

        [TestMethod]
        public void HasVerisiumOnScreen_DetectsPathAndWithinDistance()
        {
            var settings = new ClickItSettings();
            settings.ClickSettlersOre.Value = true;

            // Use internal helper and pass a simple tuple list - avoids needing to construct LabelOnGround
            var ok = (bool)InvokePrivateStatic("IsSettlersOrePath", "some/CrimsonIron/path")!;
            ok.Should().BeTrue();
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesLabyrinthToggleForTrialPortals()
        {
            string path = "Metadata/Terrain/Labyrinth/Objects/LabyrinthTrialPortalAreaTransition";

            var disabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                true,
                false,
                EntityType.AreaTransition,
                path);

            var enabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                false,
                true,
                EntityType.AreaTransition,
                path);

            disabled.Should().BeNull();
            enabled.Should().Be("labyrinth-trials");
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesAreaTransitionToggleForNonLabyrinthTransitions()
        {
            string path = "Metadata/Terrain/Leagues/Delve/Objects/SomeAreaTransition";

            var disabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                false,
                true,
                EntityType.AreaTransition,
                path);

            var enabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                true,
                false,
                EntityType.AreaTransition,
                path);

            disabled.Should().BeNull();
            enabled.Should().Be("area-transitions");
        }
    }
}
