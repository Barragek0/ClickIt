using ClickIt.Rendering;
using ClickIt.Services;
using ClickIt.Tests.TestUtils;
using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererFramesTests
    {
        [TestMethod]
        public void RenderDebugFrames_Enqueues_AltarBlockedRectangle()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            settings.DebugShowFrames.Value = true;
            plugin.__Test_SetSettings(settings);

            var areaService = new AreaService();
            var frameQueue = new DeferredFrameQueue();

            PrivateFieldAccessor.Set(areaService, "_fullScreenRectangle", new RectangleF(0, 0, 400, 300));
            PrivateFieldAccessor.Set(areaService, "_healthSquareRectangle", new RectangleF(0, 250, 80, 300));
            PrivateFieldAccessor.Set(areaService, "_flaskRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(areaService, "_skillsRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(areaService, "_manaSquareRectangle", new RectangleF(320, 250, 400, 300));
            PrivateFieldAccessor.Set(areaService, "_buffsAndDebuffsRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(areaService, "_chatPanelBlockedRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(areaService, "_mapPanelBlockedRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(areaService, "_xpBarBlockedRectangle", RectangleF.Empty);

            RectangleF targetRect = new RectangleF(250, 120, 320, 190);
            PrivateFieldAccessor.Set(areaService, "_altarBlockedRectangle", targetRect);

            var renderer = new DebugRenderer(plugin, areaService: areaService, deferredFrameQueue: frameQueue);

            renderer.RenderDebugFrames(settings);

            var frames = frameQueue.GetSnapshotForTests();
            frames.Should().Contain(f => f.Rectangle.Equals(targetRect) && f.Color.Equals(Color.Gold) && f.Thickness == 1);
        }
    }
}