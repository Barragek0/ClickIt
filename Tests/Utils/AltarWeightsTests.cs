using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class AltarWeightsTests
    {
        [TestMethod]
        public void InitializeFromArrays_NullArguments_UsesDefaultArrays()
        {
            var aw = new AltarWeights();

            aw.InitializeFromArrays(null, null, null, null);

            var topUps = aw.GetTopUpsideWeights();
            topUps.Should().HaveCount(8);
            foreach (var v in topUps) v.Should().Be(0m);

            aw["topupside", 0].Should().Be(0m);
            aw["bottomupside", 7].Should().Be(0m);
        }

        [TestMethod]
        public void Indexer_SetGet_WorksAcrossTypes()
        {
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            aw["topdownside", 1] = 11m;
            aw["bottomupside", 1] = 22m;
            aw["topupside", 1] = 33m;

            aw["topdownside", 1].Should().Be(11m);
            aw["bottomupside", 1].Should().Be(22m);
            aw["topupside", 1].Should().Be(33m);
        }

        [TestMethod]
        public void Indexer_OutOfRange_ThrowsIndexOutOfRangeException()
        {
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            Action act = () => { _ = aw["topupside", 8]; };
            act.Should().Throw<IndexOutOfRangeException>();
        }

        [TestMethod]
        public void Indexer_UnknownType_ReturnsZero()
        {
            var aw = new AltarWeights();
            aw.InitializeFromArrays(null, null, null, null);

            // unknown key should yield 0 rather than throwing
            aw["notatype", 2].Should().Be(0m);
        }

        [TestMethod]
        public void Indexer_IsCaseInsensitive()
        {
            var aw = new AltarWeights();
            aw.InitializeFromArrays(new decimal[8], new decimal[8], new decimal[8], new decimal[8]);
            aw["TopDownside", 3] = 77m; // mixed case key
            aw["topdownside", 3].Should().Be(77m);
            aw["TOPDOWNSIDE", 3].Should().Be(77m);
        }
    }
}
