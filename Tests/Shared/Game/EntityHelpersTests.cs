using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Common.Game
{
    [TestClass]
    public class EntityHelpersTests
    {
        [TestMethod]
        public void IsRitualActive_NullOrEmpty_ReturnsFalse()
        {
            EntityHelpers.IsRitualActive((ExileCore.GameController?)null).Should().BeFalse();

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            EntityHelpers.IsRitualActive(gc).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_ReturnsTrue_WhenRitualBlockerPresent()
        {
            // modifying ExileCore runtime types (which can be read-only/uninitialized).
            var paths = new System.Collections.Generic.List<string> { "SomeRitual/RitualBlocker/Node" };

            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)paths).Should().BeTrue();
        }

        [TestMethod]
        public void IsRitualActive_ReturnsFalse_WhenNoRitualBlocker()
        {
            var paths = new System.Collections.Generic.List<string?> { "Some/Thing", null, "Other/Path" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)paths).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_EmptyEnumerable_ReturnsFalse()
        {
            var empty = new System.Collections.Generic.List<string?>();
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)empty).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_AllNullEntries_ReturnsFalse()
        {
            var allNull = new System.Collections.Generic.List<string?> { null, null };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)allNull).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_IsCaseSensitive()
        {
            var lowerOnly = new System.Collections.Generic.List<string?> { "ritualblocker" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)lowerOnly).Should().BeFalse();

            var proper = new System.Collections.Generic.List<string?> { "Some/Prefix/RitualBlocker/Node" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)proper).Should().BeTrue();
        }
    }
}
