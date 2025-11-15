using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Components;
using System.Collections.Generic;

namespace ClickIt.Tests.Components
{
    [TestClass]
    public class SecondaryAltarComponentTests
    {
        [TestMethod]
        public void UpsideAndDownside_AccessorsReturnExpectedValues()
        {
            var upsides = new List<string> { "U1", "U2", "U3" };
            var downsides = new List<string> { "D1", "D2" };
            // Use the test stub constructor (upsides, downsides)
            var comp = new SecondaryAltarComponent(upsides, downsides);

            // Upside direct accessor (0..)
            for (int i = 0; i < upsides.Count; i++)
            {
                comp.GetUpsideByIndex(i).Should().Be(upsides[i]);
            }

            // Unset upside slot may be null/empty in the test stub
            comp.GetUpsideByIndex(3).Should().BeNullOrEmpty();

            // Downside direct accessor (0..)
            comp.GetDownsideByIndex(0).Should().Be(downsides[0]);
            comp.GetDownsideByIndex(1).Should().Be(downsides[1]);

            // Unset downside slot may be null/empty in the test stub
            comp.GetDownsideByIndex(2).Should().BeNullOrEmpty();

            // GetAll arrays are length 8
            comp.GetAllUpsides().Length.Should().Be(8);
            comp.GetAllDownsides().Length.Should().Be(8);
        }

        [TestMethod]
        public void GetUpsideAndDownsideByIndex_BehavePredictably()
        {
            var upsides = new List<string> { "A", "B", "C", "D", "E" };
            var downsides = new List<string> { "X", "Y", "Z" };
            var comp = new SecondaryAltarComponent(upsides, downsides);

            // Valid access
            comp.GetUpsideByIndex(0).Should().Be("A");
            comp.GetUpsideByIndex(4).Should().Be("E");
            comp.GetDownsideByIndex(0).Should().Be("X");

            // Out of range indices return empty
            comp.GetUpsideByIndex(999).Should().Be(string.Empty);
            comp.GetDownsideByIndex(-10).Should().Be(string.Empty);
        }
    }
}

