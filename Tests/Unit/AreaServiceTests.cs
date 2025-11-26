using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using SharpDX;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AreaServiceTests
    {
        [TestMethod]
        public void PointIsInClickableArea_RespectsBlockedRegions()
        {
            var svc = new AreaService();

            // Set private rectangles via reflection to avoid depending on GameController
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 160, 200, 200);
            var mana = new RectangleF(140, 160, 200, 200);
            var buffs = new RectangleF(0, 0, 100, 50);

            typeof(AreaService).GetField("_fullScreenRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, full);
            typeof(AreaService).GetField("_healthAndFlaskRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, health);
            typeof(AreaService).GetField("_manaAndSkillsRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, mana);
            typeof(AreaService).GetField("_buffsAndDebuffsRectangle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, buffs);

            // Point inside full but inside health should be excluded
            var pHealth = new Vector2(10, 170);
            svc.PointIsInClickableArea(pHealth).Should().BeFalse();

            // Point inside full and not inside any blocked rect should be allowed
            var pMain = new Vector2(50, 100);
            svc.PointIsInClickableArea(pMain).Should().BeTrue();
        }
    }
}
