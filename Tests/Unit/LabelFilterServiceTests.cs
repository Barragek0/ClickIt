using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ClickIt.Services;
using SharpDX;
using Moq;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceTests
    {
        [TestMethod]
        public void FilterHarvestLabels_ReturnsEmpty_WhenNullInput()
        {
            LabelFilterService.FilterHarvestLabels(null, _ => true).Should().BeEmpty();
        }
    }
}
