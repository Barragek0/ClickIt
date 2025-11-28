using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class EntityHelpersTests
    {
        [TestMethod]
        public void IsRitualActive_NullOrEmpty_ReturnsFalse()
        {
            // null controller (disambiguate between overloads)
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((ExileCore.GameController?)null).Should().BeFalse();

            // uninitialized GameController with no EntityListWrapper
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            global::ClickIt.Utils.EntityHelpers.IsRitualActive(gc).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_ReturnsTrue_WhenRitualBlockerPresent()
        {
            // Use the internal path-based overload to validate the logic without relying on
            // modifying ExileCore runtime types (which can be read-only/uninitialized).
            var paths = new System.Collections.Generic.List<string> { "SomeRitual/RitualBlocker/Node" };

            // This uses the internal overload added to EntityHelpers; InternalsVisibleTo allows tests access.
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)paths).Should().BeTrue();
        }

        [TestMethod]
        public void IsRitualActive_ReturnsFalse_WhenNoRitualBlocker()
        {
            var paths = new System.Collections.Generic.List<string?> { "Some/Thing", null, "Other/Path" };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)paths).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_EmptyEnumerable_ReturnsFalse()
        {
            var empty = new System.Collections.Generic.List<string?>();
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)empty).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_AllNullEntries_ReturnsFalse()
        {
            var allNull = new System.Collections.Generic.List<string?> { null, null };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)allNull).Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_IsCaseSensitive()
        {
            var lowerOnly = new System.Collections.Generic.List<string?> { "ritualblocker" };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)lowerOnly).Should().BeFalse();

            var proper = new System.Collections.Generic.List<string?> { "Some/Prefix/RitualBlocker/Node" };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)proper).Should().BeTrue();
        }
    }
}
