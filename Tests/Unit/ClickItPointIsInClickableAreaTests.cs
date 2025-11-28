using FluentAssertions;
using ClickIt.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItPointIsInClickableAreaTests
    {
        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_WhenAreaServiceMissing()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            clickIt.State.AreaService = null; // explicit

            var mi = typeof(ClickIt).GetMethod("PointIsInClickableArea", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var res = (bool)mi.Invoke(clickIt, new object[] { new Vector2(10, 10), null });
            res.Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_DelegatesToAreaService_WhenPresent()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            // Create and configure AreaService directly so we avoid GameController dependency
            var svc = new Services.AreaService();
            // Set private rectangles via reflection to define the allowed area
            var full = new SharpDX.RectangleF(0, 0, 200, 200);
            var health = new SharpDX.RectangleF(0, 300, 10, 10); // off-screen so no overlap
            var mana = new SharpDX.RectangleF(300, 300, 10, 10);
            var buffs = new SharpDX.RectangleF(300, 0, 10, 10);

            var t = typeof(Services.AreaService);
            t.GetField("_fullScreenRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, full);
            t.GetField("_healthAndFlaskRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, health);
            t.GetField("_manaAndSkillsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, mana);
            t.GetField("_buffsAndDebuffsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, buffs);

            clickIt.State.AreaService = svc;

            var mi = typeof(ClickIt).GetMethod("PointIsInClickableArea", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // choose a point inside the full screen rectangle and not in any blocked area
            var p = new Vector2(20, 20);
            var res = (bool)mi.Invoke(clickIt, new object[] { p, null });
            res.Should().BeTrue();
        }
    }
}
