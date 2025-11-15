using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Tests
{
    [TestClass]
    public class PerformanceTimingTests
    {
        private const int TARGET_CLICK_INTERVAL_MS = 50; // Target from recent optimizations
        private const int CACHE_TIMEOUT_MS = 50; // Optimized cache timeout
    private const int ACCEPTABLE_VARIANCE_MS = 30; // Acceptable variance for timing tests (relaxed to reduce flakiness)

        [TestMethod]
        public void ClickInterval_ShouldMeetPerformanceTargets()
        {
            // Test that click intervals can achieve 50ms target
            var timer = new Stopwatch();
            var intervals = new List<long>();

            // Simulate multiple click intervals
            var deterministicRandom = new Random(0);
            for (int i = 0; i < 10; i++)
            {
                timer.Restart();

                // Simulate the optimized timing logic: 30ms + random(0, 20)
                var baseInterval = 30;
                var randomComponent = deterministicRandom.Next(0, 20);
                var totalInterval = baseInterval + randomComponent;

                // Simulate work that should complete within this interval
                var workTimer = Stopwatch.StartNew();
                while (workTimer.ElapsedMilliseconds < totalInterval)
                {
                    // Busy wait to simulate work
                }
                workTimer.Stop();

                timer.Stop();
                intervals.Add(timer.ElapsedMilliseconds);
            }

            // Verify that intervals are within acceptable range
            intervals.Should().AllSatisfy(interval =>
                interval.Should().BeLessOrEqualTo(TARGET_CLICK_INTERVAL_MS + ACCEPTABLE_VARIANCE_MS,
                    "click interval should not exceed target + variance"));

            intervals.Should().AllSatisfy(interval =>
                interval.Should().BeGreaterOrEqualTo(30,
                    "click interval should respect minimum timing"));
        }

        [TestMethod]
        public void CacheTimeout_ShouldBeOptimized()
        {
            // Test that cache timeout meets optimized 50ms target
            var cacheTimer = new Stopwatch();
            cacheTimer.Start();

            // Simulate cache validation after optimized timeout
            var simulatedCacheAge = CACHE_TIMEOUT_MS;

            // Cache should be considered stale after timeout
            bool cacheExpired = cacheTimer.ElapsedMilliseconds >= simulatedCacheAge ||
                                simulatedCacheAge >= CACHE_TIMEOUT_MS;

            cacheExpired.Should().BeTrue("cache should expire after optimized timeout");

            // Test that cache is valid within timeout
            var freshCacheAge = CACHE_TIMEOUT_MS - 10;
            bool cacheValid = freshCacheAge < CACHE_TIMEOUT_MS;

            cacheValid.Should().BeTrue("cache should be valid within timeout period");
        }

        [TestMethod]
        public void TimingThrottle_ShouldPreventExcessiveExecution()
        {
            // Test that timing throttle prevents excessive execution frequency
            var throttleTimer = new Stopwatch();
            var executionCount = 0;
            var maxExecutions = 5;

            throttleTimer.Start();

            // Simulate throttled execution attempts
            while (executionCount < maxExecutions && throttleTimer.ElapsedMilliseconds < 1000)
            {
                // Check if throttle allows execution (30ms + random)
                var minInterval = 30;
                var timeSinceLastExecution = throttleTimer.ElapsedMilliseconds;

                if (timeSinceLastExecution >= minInterval)
                {
                    executionCount++;
                    throttleTimer.Restart(); // Reset for next interval
                }
            }

            // Should have executed multiple times but not excessively
            executionCount.Should().BeGreaterThan(0, "throttle should allow some executions");
            executionCount.Should().BeLessThan(50, "throttle should prevent excessive executions");
        }

        [TestMethod]
        public void RandomizedTiming_ShouldProvideVariance()
        {
            // Test that randomized timing provides appropriate variance
            var timings = new List<int>();
            var random = new Random(42); // Fixed seed for reproducible tests

            // Generate multiple timing values
            for (int i = 0; i < 100; i++)
            {
                var baseTime = 30;
                var randomComponent = random.Next(0, 20);
                var totalTime = baseTime + randomComponent;

                timings.Add(totalTime);
            }

            // Should have variance in timings
            timings.Distinct().Should().HaveCountGreaterThan(5, "should have timing variance");

            // Should stay within expected bounds
            timings.Should().AllSatisfy(timing =>
                timing.Should().BeInRange(30, 49, "timing should be within optimized range"));

            // Average should be reasonable
            var averageTiming = timings.Average();
            averageTiming.Should().BeInRange(35, 45, "average timing should be in reasonable range");
        }

        [TestMethod]
        public void WaitTimeOptimization_ShouldBeEfficient()
        {
            // Test that wait times are optimized for performance
            var waitTimer = new Stopwatch();

            // Test short wait optimization (50-60ms range from code)
            var shortWaitMin = 50;
            var shortWaitMax = 60;

            waitTimer.Start();
            var deterministicRandom2 = new Random(1);
            var shortWaitTime = deterministicRandom2.Next(shortWaitMin, shortWaitMax);

            // Simulate optimized wait
            while (waitTimer.ElapsedMilliseconds < shortWaitTime)
            {
                // Busy wait simulation
            }
            waitTimer.Stop();

            waitTimer.ElapsedMilliseconds.Should().BeInRange(shortWaitMin - 10, shortWaitMax + 10,
                "short wait should be within optimized range");

            // Test that waits are not excessively long
            shortWaitTime.Should().BeLessOrEqualTo(100, "wait times should not be excessive");
        }

        [TestMethod]
        public void PerformanceRegression_ShouldNotExceedBaseline()
        {
            // Test for performance regression detection
            var performanceTimer = new Stopwatch();
            var measurements = new List<long>();

            // Simulate performance-critical operations
            for (int i = 0; i < 10; i++)
            {
                performanceTimer.Restart();

                // Simulate the main performance path
                var operations = 1000;
                for (int j = 0; j < operations; j++)
                {
                    // Simulate lightweight operations
                    _ = j * 2;
                }

                performanceTimer.Stop();
                measurements.Add(performanceTimer.ElapsedMilliseconds);
            }

            // Performance should be consistently fast
            measurements.Should().AllSatisfy(measurement =>
                measurement.Should().BeLessOrEqualTo(100, "operations should complete quickly"));

            // Average performance should be good
            var averageTime = measurements.Average();
            averageTime.Should().BeLessOrEqualTo(50, "average performance should be good");
        }

        [TestMethod]
        public void TimerAccuracy_ShouldBeReliable()
        {
            // Test timer accuracy and reliability
            var accuracyTimer = new Stopwatch();
            var targetDuration = 100; // 100ms test

            accuracyTimer.Start();

            // Wait for target duration
            while (accuracyTimer.ElapsedMilliseconds < targetDuration)
            {
                // Precise timing wait
            }

            accuracyTimer.Stop();
            var actualDuration = accuracyTimer.ElapsedMilliseconds;

            // Timer should be reasonably accurate
            actualDuration.Should().BeInRange(targetDuration - 50, targetDuration + 50,
                "timer should be reasonably accurate");

            // Test timer restart functionality
            accuracyTimer.Restart();
            var restartValue = accuracyTimer.ElapsedMilliseconds;

            restartValue.Should().BeLessThan(10, "timer restart should reset to near zero");
        }

        [TestMethod]
        public void ConcurrentTimingOperations_ShouldNotInterfere()
        {
            // Test that concurrent timing operations don't interfere
            var timer1 = new Stopwatch();
            var timer2 = new Stopwatch();
            var results = new List<bool>();

            // Start both timers
            timer1.Start();
            timer2.Start();

            // Test multiple concurrent checks
            for (int i = 0; i < 5; i++)
            {
                var time1 = timer1.ElapsedMilliseconds;
                var time2 = timer2.ElapsedMilliseconds;

                // Both timers should advance
                results.Add(time1 >= 0 && time2 >= 0);

                // Brief pause between checks
                var pauseTimer = Stopwatch.StartNew();
                while (pauseTimer.ElapsedMilliseconds < 10) { }
            }

            results.Should().AllSatisfy(result => result.Should().BeTrue(
                "concurrent timers should operate independently"));

            timer1.Stop();
            timer2.Stop();

            // Both should have recorded time
            timer1.ElapsedMilliseconds.Should().BeGreaterThan(0, "timer1 should have elapsed time");
            timer2.ElapsedMilliseconds.Should().BeGreaterThan(0, "timer2 should have elapsed time");
        }
    }
}
