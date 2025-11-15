using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Components;

namespace ClickIt.Tests.Decision
{
    [TestClass]
    public class WeightCalculatorMoreTests
    {
        [TestMethod]
        public void CalculateAltarWeights_MultipleUpsidesSum()
        {
            // Arrange - use lightweight test settings and the existing WeightCalculator
            var calc = ClickIt.Tests.Shared.TestHelpers.CreateWeightCalculator(new System.Collections.Generic.Dictionary<string, int>
            {
                // ARCHIVE: original WeightCalculatorMoreTests.cs moved into Decision/WeightCalculatorTests.cs
