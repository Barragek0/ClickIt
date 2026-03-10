using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using ClickIt.Tests.TestUtils;
using SharpDX;

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

            SetRectangles(svc, full, health, mana, buffs);

            // Point inside full but inside health should be excluded
            var pHealth = new Vector2(10, 170);
            svc.PointIsInClickableArea(pHealth).Should().BeFalse();

            // Point inside full and not inside any blocked rect should be allowed
            var pMain = new Vector2(50, 100);
            svc.PointIsInClickableArea(pMain).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsTrue_InsideFullScreen_NotInBlockedAreas()
        {
            var svc = new AreaService();

            // set private rectangles to known values
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 180, 20, 20); // bottom-left zone
            var mana = new RectangleF(180, 180, 20, 20); // bottom-right zone
            var buffs = new RectangleF(0, 0, 30, 30); // top-left zone

            SetRectangles(svc, full, health, mana, buffs);

            // point in center not inside any blocked zones
            var p = new Vector2(100, 100);
            svc.PointIsInClickableArea(p).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_WhenBlockedAreaCoversFullScreen()
        {
            var svc = new AreaService();
            var full = new RectangleF(0, 0, 200, 200);
            // set one blocked area to cover the full screen - this guarantees clicks are disallowed
            var health = new RectangleF(0, 0, 200, 200);
            var mana = new RectangleF(0, 0, 0, 0);
            var buffs = new RectangleF(0, 0, 0, 0);

            SetRectangles(svc, full, health, mana, buffs);

            var insideHealth = new Vector2(1, 181);
            svc.PointIsInClickableArea(insideHealth).Should().BeFalse();

            var insideMana = new Vector2(190, 190);
            svc.PointIsInClickableArea(insideMana).Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_BorderCases_BehaveConsistently()
        {
            var svc = new AreaService();
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 100, 20, 20);
            var mana = new RectangleF(180, 100, 20, 20);
            var buffs = new RectangleF(0, 0, 30, 30);

            SetRectangles(svc, full, health, mana, buffs);

            // a point exactly on the full-screen boundary should still be treated consistently
            var edge = new Vector2(200, 100);
            // Ensure calling this doesn't throw - behaviour at the edge may be implementation defined
            System.Action act = () => svc.PointIsInClickableArea(edge);
            act.Should().NotThrow();

            // a point outside full screen -> false
            svc.PointIsInClickableArea(new Vector2(-1, -1)).Should().BeFalse();
        }

        private static void SetRectangles(AreaService svc, RectangleF full, RectangleF health, RectangleF mana, RectangleF buffs)
        {
            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", full);
            PrivateFieldAccessor.Set(svc, "_healthAndFlaskRectangle", health);
            PrivateFieldAccessor.Set(svc, "_manaAndSkillsRectangle", mana);
            PrivateFieldAccessor.Set(svc, "_buffsAndDebuffsRectangle", buffs);
        }
    }
}
