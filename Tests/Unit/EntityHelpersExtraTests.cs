using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class EntityHelpersExtraTests
    {
        [TestMethod]
        public void IsRitualActive_EmptyEnumerable_ReturnsFalse()
        {
            var empty = new System.Collections.Generic.List<string?>();
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)empty)
                .Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_AllNullEntries_ReturnsFalse()
        {
            var allNull = new System.Collections.Generic.List<string?> { null, null };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)allNull)
                .Should().BeFalse();
        }

        [TestMethod]
        public void IsRitualActive_IsCaseSensitive()
        {
            var lowerOnly = new System.Collections.Generic.List<string?> { "ritualblocker" };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)lowerOnly)
                .Should().BeFalse();

            var proper = new System.Collections.Generic.List<string?> { "Some/Prefix/RitualBlocker/Node" };
            global::ClickIt.Utils.EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)proper)
                .Should().BeTrue();
        }
    }
}
