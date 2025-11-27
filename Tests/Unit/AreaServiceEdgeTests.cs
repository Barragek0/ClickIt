using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using SharpDX;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AreaServiceEdgeTests
    {
        [TestMethod]
        public void PointIsInClickableArea_ReturnsTrue_InsideFullScreen_NotInBlockedAreas()
        {
            var svc = new AreaService();

            // set private rectangles to known values
            var t = typeof(AreaService);
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 180, 20, 20); // bottom-left zone
            var mana = new RectangleF(180, 180, 20, 20); // bottom-right zone
            var buffs = new RectangleF(0, 0, 30, 30); // top-left zone

            t.GetField("_fullScreenRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, full);
            t.GetField("_healthAndFlaskRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, health);
            t.GetField("_manaAndSkillsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, mana);
            t.GetField("_buffsAndDebuffsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, buffs);

            // point in center not inside any blocked zones
            var p = new Vector2(100, 100);
            svc.PointIsInClickableArea(p).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_WhenPointInsideBlockedArea()
        {
            var svc = new AreaService();
            var t = typeof(AreaService);
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 180, 20, 20);
            var mana = new RectangleF(180, 180, 20, 20);
            var buffs = new RectangleF(0, 0, 30, 30);

            t.GetField("_fullScreenRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, full);
            t.GetField("_healthAndFlaskRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, health);
            t.GetField("_manaAndSkillsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, mana);
            t.GetField("_buffsAndDebuffsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, buffs);

            var insideHealth = new Vector2(1, 181);
            svc.PointIsInClickableArea(insideHealth).Should().BeFalse();

            var insideMana = new Vector2(190, 190);
            svc.PointIsInClickableArea(insideMana).Should().BeFalse();

            var insideBuffs = new Vector2(5, 5);
            svc.PointIsInClickableArea(insideBuffs).Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_BorderCases_BehaveConsistently()
        {
            var svc = new AreaService();
            var t = typeof(AreaService);
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 100, 20, 20);
            var mana = new RectangleF(180, 100, 20, 20);
            var buffs = new RectangleF(0, 0, 30, 30);

            t.GetField("_fullScreenRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, full);
            t.GetField("_healthAndFlaskRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, health);
            t.GetField("_manaAndSkillsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, mana);
            t.GetField("_buffsAndDebuffsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(svc, buffs);

            // Point exactly on the edge of full screen should still be considered "in" (PointInRectangle includes boundaries)
            var edge = new Vector2(200, 100);
            svc.PointIsInClickableArea(edge).Should().BeFalse(); // edge is in full rect but also within mana or out-of-bounds depending on implementation

            // a point outside full screen -> false
            svc.PointIsInClickableArea(new Vector2(-1, -1)).Should().BeFalse();
        }
    }
}
