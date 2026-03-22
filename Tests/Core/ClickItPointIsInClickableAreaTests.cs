using FluentAssertions;
using ClickIt.Services;
using ClickIt.Tests.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItPointIsInClickableAreaTests
    {
        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_WhenAreasAreNotInitialized()
        {
            var svc = new AreaService();
            var res = svc.PointIsInClickableArea(null, new Vector2(10, 10));
            res.Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_UsesAreaService_WhenPresent()
        {
            // Create and configure AreaService directly so we avoid GameController dependency
            var svc = new AreaService();
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 300, 10, 10); // off-screen so no overlap
            var mana = new RectangleF(300, 300, 10, 10);
            var buffs = new RectangleF(300, 0, 10, 10);

            SetAreaRectangles(svc, full, health, mana, buffs);

            var p = new Vector2(20, 20);
            var res = svc.PointIsInClickableArea(null, p);
            res.Should().BeTrue();
        }

        private static void SetAreaRectangles(AreaService svc, RectangleF full, RectangleF health, RectangleF mana, RectangleF buffs)
        {
            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", full);
            PrivateFieldAccessor.Set(svc, "_healthAndFlaskRectangle", health);
            PrivateFieldAccessor.Set(svc, "_manaAndSkillsRectangle", mana);
            PrivateFieldAccessor.Set(svc, "_buffsAndDebuffsRectangle", buffs);
        }
    }
}
