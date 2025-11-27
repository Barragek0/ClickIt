using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using FluentAssertions;
using ClickIt;
using ClickIt.Rendering;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxRendererSeamsTests
    {
        [TestMethod]
        public void GetEnqueuedFramesForTests_Returns_PreEnqueuedFrames()
        {
            var queue = new DeferredFrameQueue();

            var r1 = new RectangleF(1, 2, 3, 4);
            var r2 = new RectangleF(5, 6, 7, 8);
            queue.Enqueue(r1, Color.Red, 2);
            queue.Enqueue(r2, Color.LawnGreen, 3);

            var settings = new ClickItSettings();
            var renderer = new StrongboxRenderer(settings, queue);

            var frames = renderer.GetEnqueuedFramesForTests();
            frames.Should().HaveCount(2);
            frames[0].Rectangle.Should().Be(r1);
            frames[0].Color.Should().Be(Color.Red);
            frames[0].Thickness.Should().Be(2);
            frames[1].Rectangle.Should().Be(r2);
            frames[1].Color.Should().Be(Color.LawnGreen);
            frames[1].Thickness.Should().Be(3);
        }

        [TestMethod]
        public void GetEnabledStrongboxKeys_Returns_AllEnabledByDefault()
        {
            var settings = new ClickItSettings();
            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);

            var method = typeof(StrongboxRenderer).GetMethod("GetEnabledStrongboxKeys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keys = (System.Collections.Generic.List<string>)method.Invoke(renderer, Array.Empty<object>());

            keys.Should().NotBeNull();
            keys.Should().Contain("StrongBoxes/Strongbox");
            keys.Should().Contain("StrongBoxes/Arcanist");
            keys.Should().Contain("StrongBoxes/Armory");
            keys.Count.Should().BeGreaterThan(0);
        }

        [DataTestMethod]
        [DataRow("abc/StrongBoxes/strongbox/somepath", true)]
        [DataRow("no/match/here", false)]
        [DataRow("path/StrongBoxes/Arcanist/thing", true)]
        public void IsStrongboxClickableBySettings_VariousPaths(string path, bool expect)
        {
            var settings = new ClickItSettings();
            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);

            var getKeys = typeof(StrongboxRenderer).GetMethod("GetEnabledStrongboxKeys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enabledKeys = (System.Collections.Generic.List<string>)getKeys.Invoke(renderer, Array.Empty<object>());

            var isClickable = typeof(StrongboxRenderer).GetMethod("IsStrongboxClickableBySettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var res = (bool)isClickable.Invoke(null, new object[] { path, enabledKeys });

            res.Should().Be(expect);
        }
    }
}
