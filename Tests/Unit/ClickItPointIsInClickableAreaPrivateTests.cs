using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Tests.TestUtils;
using SharpDX;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItPointIsInClickableAreaPrivateTests
    {
        [TestMethod]
        public void PointIsInClickableArea_UsesAreaServiceFields()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            // inject AreaService and set private rectangles so the test point is inside full-screen and outside the UI zones
            var area = new Services.AreaService();
            SetAreaRectangles(
                area,
                new RectangleF(0, 0, 400, 300),
                new RectangleF(0, 250, 100, 300),
                new RectangleF(300, 250, 400, 300),
                new RectangleF(0, 0, 50, 50));

            plugin.State.AreaService = area;

            var mi = plugin.GetType().GetMethod("PointIsInClickableArea", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // This point is in the middle of the screen and should be considered clickable
            var point = new Vector2(200f, 150f);
            var res = (bool)mi.Invoke(plugin, new object[] { point, null })!;
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
