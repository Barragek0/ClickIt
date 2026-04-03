using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Core
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
            svc.ApplyBlockedSnapshot(new AreaBlockedSnapshot
            {
                FullScreenRectangle = full,
                HealthAndFlaskRectangle = health,
                ManaAndSkillsRectangle = mana,
                HealthSquareRectangle = health,
                ManaSquareRectangle = mana,
                BuffsAndDebuffsRectangle = buffs
            });
        }
    }
}
