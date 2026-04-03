using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.UI
{
    [TestClass]
    public class DebugOverlayComposerTests
    {
        [TestMethod]
        public void RenderSections_RendersOnlyEnabledSections_InOrder()
        {
            var engine = new DebugLayoutEngine();
            var settings = new DebugLayoutSettings(120, 18, 34, 4, 10, 600);
            var composer = new DebugOverlayComposer(engine, settings);
            List<string> calls = [];

            DebugOverlaySection[] sections =
            [
                new(true, (x, y, h) =>
                {
                    calls.Add($"A:{x}:{y}:{h}");
                    return (x, y + h);
                }),
                new(false, (x, y, h) =>
                {
                    calls.Add("B");
                    return (x, y + h);
                }),
                new(true, (x, y, h) =>
                {
                    calls.Add($"C:{x}:{y}:{h}");
                    return (x, y + (2 * h));
                })
            ];

            composer.RenderSections(sections);

            calls.Should().HaveCount(2);
            calls[0].Should().StartWith("A:");
            calls[1].Should().StartWith("C:");
        }
    }
}