using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
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

            // set private backing fields (we don't want to call UpdateScreenAreas)
            var t = area.GetType();
            var fullField = t.GetField("_fullScreenRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var hField = t.GetField("_healthAndFlaskRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var mField = t.GetField("_manaAndSkillsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var bField = t.GetField("_buffsAndDebuffsRectangle", BindingFlags.NonPublic | BindingFlags.Instance)!;

            fullField.SetValue(area, new RectangleF(0, 0, 400, 300));
            hField.SetValue(area, new RectangleF(0, 250, 100, 300));
            mField.SetValue(area, new RectangleF(300, 250, 400, 300));
            bField.SetValue(area, new RectangleF(0, 0, 50, 50));

            // attach area service to plugin state
            var stateField = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var state = stateField.GetValue(plugin);
            var asField = state.GetType().GetProperty("AreaService", BindingFlags.Public | BindingFlags.Instance)!;
            asField.SetValue(state, area);

            var mi = plugin.GetType().GetMethod("PointIsInClickableArea", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // This point is in the middle of the screen and should be considered clickable
            var point = new Vector2(200f, 150f);
            var res = (bool)mi.Invoke(plugin, new object[] { point, null })!;
            res.Should().BeTrue();
        }
    }
}
