using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using ClickIt.Utils;
using ClickIt.Tests.TestUtils;
using ClickIt.Components;
using System.Linq;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarRepositoryTests
    {
        [TestMethod]
        public void AddAltarComponent_AddsUniqueAndPreventsDuplicates()
        {
            var repo = new AltarRepository();

            var compA = TestBuilders.BuildPrimary(
                TestBuilders.BuildSecondary(new string[] { "a1", "a2" }.Concat(Enumerable.Repeat("", 6)).ToArray()),
                TestBuilders.BuildSecondary(new string[] { "b1", "b2" }.Concat(Enumerable.Repeat("", 6)).ToArray())
            );

            bool addedFirst = repo.AddAltarComponent(compA);
            bool addedSecond = repo.AddAltarComponent(compA);

            addedFirst.Should().BeTrue();
            addedSecond.Should().BeFalse();
            repo.GetAltarComponents().Count.Should().Be(1);
        }

        [TestMethod]
        public void ClearAltarComponents_ClearsListAndInvalidatesCaches()
        {
            var repo = new AltarRepository();
            var comp = TestBuilders.BuildPrimary();

            comp.GetCachedWeights(p => new AltarWeights { TopUpsideWeight = 5, TopDownsideWeight = 1 });

            repo.AddAltarComponent(comp);
            repo.GetAltarComponents().Should().HaveCount(1);

            repo.ClearAltarComponents();
            repo.GetAltarComponents().Should().BeEmpty();

            var cached = PrivateFieldAccessor.Get<object?>(comp, "_cachedWeights");
            cached.Should().BeNull();
        }
    }
}
