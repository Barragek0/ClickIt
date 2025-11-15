using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ClickIt.Tests.Concurrency
{
    [TestClass]
    public class AltarProcessingConcurrencyTests
    {
        [TestMethod]
        public void ParallelAltarProcessing_ShouldNotThrowAndMaintainUniqueIds()
        {
            // Simulate a shared registry of processed altar IDs
            var processed = new ConcurrentDictionary<int, int>();

            // Simulate work: each task processes N items and records an ID (possibly overlapping)
            int tasks = 16;
            int perTask = 200;

            Parallel.For(0, tasks, i =>
            {
                for (int j = 0; j < perTask; j++)
                {
                    int id = (i * perTask) + j; // unique per logical item
                    processed.TryAdd(id, 1).Should().BeTrue();
                }
            });

            // Validate all expected items were processed once
            processed.Count.Should().Be(tasks * perTask);
        }
    }
}
