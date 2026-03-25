using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using ClickIt.Utils;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsDirectTests
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

            var backing = type.GetField($"<{memberName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backing != null)
            {
                backing.SetValue(obj, value);
                return;
            }

            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name.IndexOf(memberName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    f.SetValue(obj, value);
                    return;
                }
            }

            // Fallback: set first field of matching type
            if (value != null)
            {
                foreach (var f in fields)
                {
                    if (f.FieldType.IsInstanceOfType(value))
                    {
                        f.SetValue(obj, value);
                        return;
                    }
                }
            }

            throw new InvalidOperationException($"Unable to set member '{memberName}' on type '{type.FullName}' in test.");
        }

        [TestMethod]
        public void IsValidClickableLabel_NullOrMissingParts_ReturnsFalse()
        {
            LabelUtils.IsValidClickableLabel(null!, (v) => true).Should().BeFalse();
        }

        [TestMethod]
        public void IsValidEntityPath_DetectsClickablePathAndHandlesNull()
        {
            var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            SetMemberValue(ent, "Path", null);
            LabelUtils.IsValidEntityPath(ent).Should().BeFalse();

            SetMemberValue(ent, "Path", "some/thing/PetrifiedWood/abc");
            LabelUtils.IsValidEntityPath(ent).Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelForTests_HarvestRequiresVisibleRootElement()
        {
            var clickableHarvestPath = "Metadata/Terrain/Leagues/Harvest/Irrigator";

            var blocked = LabelUtils.IsValidClickableLabelForTests(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.WorldItem,
                path: clickableHarvestPath,
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: false);

            blocked.Should().BeFalse();

            var allowed = LabelUtils.IsValidClickableLabelForTests(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.WorldItem,
                path: clickableHarvestPath,
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: true);

            allowed.Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelForTests_NonHarvestIgnoresRootElementVisibility()
        {
            var nonHarvestPath = "Metadata/Terrain/Leagues/Ritual/SomeObject";

            var result = LabelUtils.IsValidClickableLabelForTests(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.WorldItem,
                path: nonHarvestPath,
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: false);

            result.Should().BeTrue();
        }

    }
}
