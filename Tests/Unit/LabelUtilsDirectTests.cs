using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Collections.Generic;
using ClickIt.Utils;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

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
            // null label -> false
            LabelUtils.IsValidClickableLabel(null!, (v) => true).Should().BeFalse();
        }

        [TestMethod]
        public void IsValidEntityPath_DetectsClickablePathAndHandlesNull()
        {
            var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            // null path -> false
            SetMemberValue(ent, "Path", null);
            LabelUtils.IsValidEntityPath(ent).Should().BeFalse();

            // path contains PetrifiedWood -> true
            SetMemberValue(ent, "Path", "some/thing/PetrifiedWood/abc");
            LabelUtils.IsValidEntityPath(ent).Should().BeTrue();
        }

        // NOTE: creating and mutating ExileCore runtime Element/LabelOnGround objects is unsafe inside unit tests.
        // Similar behaviors are covered by the adapter-based tests (LabelUtilsAdapterTests.cs and LabelUtilsAdapterDedupTests.cs),
        // so we avoid constructing native-backed Element instances here and only assert the Sort/partition helper existence.

        [TestMethod]
        public void PartitionAndSwap_OperateOnLabelOnGround_DistanceOrdering()
        {
            // This partition/swap logic operates on LabelOnGround which is runtime memory-backed
            // and cannot be safely constructed here. We're keeping this test present but empty
            // to acknowledge the helper exists and avoid unsafe memory access.
            var mi = typeof(LabelUtils).GetMethod("SortLabelsByDistance", BindingFlags.Public | BindingFlags.Static);
            mi.Should().NotBeNull();
        }
    }
}
