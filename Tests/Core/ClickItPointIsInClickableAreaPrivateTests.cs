using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Tests.TestUtils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItPointIsInClickableAreaPrivateTests
    {
        [TestMethod]
        public void PointIsInClickableArea_UsesAreaServiceFields()
        {
            var area = new Services.AreaService();
            SetAreaRectangles(
                area,
                new RectangleF(0, 0, 400, 300),
                new RectangleF(0, 250, 100, 300),
                new RectangleF(300, 250, 400, 300),
                new RectangleF(0, 0, 50, 50));

            var point = new Vector2(200f, 150f);
            var res = area.PointIsInClickableArea(null, point);
            res.Should().BeTrue();
        }

        private static void SetAreaRectangles(Services.AreaService svc, RectangleF full, RectangleF health, RectangleF mana, RectangleF buffs)
        {
            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", full);
            PrivateFieldAccessor.Set(svc, "_healthAndFlaskRectangle", health);
            PrivateFieldAccessor.Set(svc, "_manaAndSkillsRectangle", mana);
            PrivateFieldAccessor.Set(svc, "_buffsAndDebuffsRectangle", buffs);
        }
    }
}
