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
        public void PointIsInClickableArea_ReturnsFalse_WhenAreaServiceMissing()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            clickIt.State.AreaService = null; // explicit

            var res = PrivateMethodAccessor.Invoke<bool>(clickIt, "PointIsInClickableArea", new Vector2(10, 10), null);
            res.Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_DelegatesToAreaService_WhenPresent()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            // Create and configure AreaService directly so we avoid GameController dependency
            var svc = new AreaService();
            // Set private rectangles via reflection to define the allowed area
            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 300, 10, 10); // off-screen so no overlap
            var mana = new RectangleF(300, 300, 10, 10);
            var buffs = new RectangleF(300, 0, 10, 10);

            SetAreaRectangles(svc, full, health, mana, buffs);

            clickIt.State.AreaService = svc;

            // choose a point inside the full screen rectangle and not in any blocked area
            var p = new Vector2(20, 20);
            var res = PrivateMethodAccessor.Invoke<bool>(clickIt, "PointIsInClickableArea", p, null);
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
