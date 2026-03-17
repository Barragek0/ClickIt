using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using ClickIt.Tests.TestUtils;
using SharpDX;
using System.Collections.Generic;

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

            var pHealth = new Vector2(10, 170);
            svc.PointIsInClickableArea(pHealth).Should().BeFalse();

            var pMain = new Vector2(50, 100);
            svc.PointIsInClickableArea(pMain).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsTrue_InsideFullScreen_NotInBlockedAreas()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 200, 200);
            var health = new RectangleF(0, 180, 20, 20); // bottom-left zone
            var mana = new RectangleF(180, 180, 20, 20); // bottom-right zone
            var buffs = new RectangleF(0, 0, 30, 30); // top-left zone

            SetRectangles(svc, full, health, mana, buffs);

            var p = new Vector2(100, 100);
            svc.PointIsInClickableArea(p).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_WhenBlockedAreaCoversFullScreen()
        {
            var svc = new AreaService();
            var full = new RectangleF(0, 0, 200, 200);
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

            var edge = new Vector2(200, 100);
            System.Action act = () => svc.PointIsInClickableArea(edge);
            act.Should().NotThrow();

            svc.PointIsInClickableArea(new Vector2(-1, -1)).Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InQuestTrackerBlockedRectangles()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var questTrackerBlocks = new List<RectangleF>
            {
                new RectangleF(300, 50, 320, 110),
                new RectangleF(330, 50, 350, 110)
            };

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_questTrackerBlockedRectangles", questTrackerBlocks);

            svc.PointIsInClickableArea(new Vector2(310, 80)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(340, 80)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(260, 80)).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRetainQuestTrackerRectanglesOnEmptyRead_ReturnsTrue_WithinHoldWindow()
        {
            bool retain = AreaService.ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                currentRectangleCount: 2,
                now: 1_100,
                lastSuccessTimestampMs: 1_000,
                holdLastGoodMs: 200);

            retain.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRetainQuestTrackerRectanglesOnEmptyRead_ReturnsFalse_AfterHoldWindow()
        {
            bool retain = AreaService.ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                currentRectangleCount: 2,
                now: 1_250,
                lastSuccessTimestampMs: 1_000,
                holdLastGoodMs: 200);

            retain.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsFalse_InsideRefreshWindow()
        {
            bool shouldRefresh = AreaService.ShouldRefreshBlockedUiRectangles(
                now: 10_500,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 10_000);

            shouldRefresh.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsTrue_WhenIntervalElapsed()
        {
            bool shouldRefresh = AreaService.ShouldRefreshBlockedUiRectangles(
                now: 20_000,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 10_000);

            shouldRefresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsTrue_OnFirstRefresh()
        {
            bool shouldRefresh = AreaService.ShouldRefreshBlockedUiRectangles(
                now: 10_500,
                lastRefreshTimestampMs: 0,
                refreshIntervalMs: 10_000);

            shouldRefresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsTrue_WhenForceRefreshRequested()
        {
            bool shouldRefresh = AreaService.ShouldRefreshBlockedUiRectangles(
                now: 10_500,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 10_000,
                forceRefresh: true);

            shouldRefresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_Supports500MsWindow_ForBuffsAndDebuffsRefresh()
        {
            bool shouldRefreshTooSoon = AreaService.ShouldRefreshBlockedUiRectangles(
                now: 10_300,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 500);

            bool shouldRefreshOnTime = AreaService.ShouldRefreshBlockedUiRectangles(
                now: 10_500,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 500);

            shouldRefreshTooSoon.Should().BeFalse();
            shouldRefreshOnTime.Should().BeTrue();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsTrue_OnFirstKnownArea()
        {
            bool changed = AreaService.HasAreaHashChanged(
                currentAreaHash: 123,
                lastKnownAreaHash: long.MinValue);

            changed.Should().BeTrue();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsTrue_WhenAreaChanges()
        {
            bool changed = AreaService.HasAreaHashChanged(
                currentAreaHash: 124,
                lastKnownAreaHash: 123);

            changed.Should().BeTrue();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsFalse_WhenAreaStaysSame()
        {
            bool changed = AreaService.HasAreaHashChanged(
                currentAreaHash: 123,
                lastKnownAreaHash: 123);

            changed.Should().BeFalse();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsFalse_WhenCurrentHashUnknown()
        {
            bool changed = AreaService.HasAreaHashChanged(
                currentAreaHash: long.MinValue,
                lastKnownAreaHash: 123);

            changed.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUpdateMapPanelBlockedRectangle_ReturnsFalse_InTownOrHideout()
        {
            bool shouldUpdate = AreaService.ShouldUpdateMapPanelBlockedRectangle(isInTownOrHideout: true);

            shouldUpdate.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUpdateMapPanelBlockedRectangle_ReturnsTrue_WhenInMapArea()
        {
            bool shouldUpdate = AreaService.ShouldUpdateMapPanelBlockedRectangle(isInTownOrHideout: false);

            shouldUpdate.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldUseVisibleUiBlockedRectangle_ReturnsTrue_OnlyWhenValidAndVisible()
        {
            AreaService.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: true).Should().BeTrue();
            AreaService.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: false).Should().BeFalse();
            AreaService.ShouldUseVisibleUiBlockedRectangle(elementIsValid: false, elementIsVisible: true).Should().BeFalse();
            AreaService.ShouldUseVisibleUiBlockedRectangle(elementIsValid: false, elementIsVisible: false).Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InChatPanelBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var chatBlocked = new RectangleF(40, 220, 180, 280);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_chatPanelBlockedRectangle", chatBlocked);

            svc.PointIsInClickableArea(new Vector2(80, 240)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(200, 240)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InMapPanelBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var mapBlocked = new RectangleF(300, 0, 390, 120);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_mapPanelBlockedRectangle", mapBlocked);

            svc.PointIsInClickableArea(new Vector2(340, 60)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(250, 60)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InXpBarBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var xpBarBlocked = new RectangleF(120, 120, 220, 200);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_xpBarBlockedRectangle", xpBarBlocked);

            svc.PointIsInClickableArea(new Vector2(180, 150)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(230, 150)).Should().BeTrue();
        }

        [TestMethod]
        public void ExtendRectUpwardLtrb_ExpandsTopAndHeight()
        {
            RectangleF rect = new RectangleF(120, 120, 220, 200);

            RectangleF extended = AreaService.ExtendRectUpwardLtrb(rect, 5f);

            extended.X.Should().Be(120);
            extended.Y.Should().Be(115);
            extended.Width.Should().Be(220);
            extended.Height.Should().Be(205);
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InXpBarTopExtensionStrip()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var xpBarBlocked = AreaService.ExtendRectUpwardLtrb(new RectangleF(120, 120, 220, 200), 5f);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_xpBarBlockedRectangle", xpBarBlocked);

            svc.PointIsInClickableArea(new Vector2(180, 117)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(180, 114)).Should().BeTrue();
        }

        [TestMethod]
        public void PointInRectangleEitherSpace_ReturnsTrue_ForScreenPointAgainstClientRect()
        {
            var rect = new RectangleF(120, 120, 220, 200);
            var windowTopLeft = new Vector2(100, 200);
            var screenPoint = new Vector2(250, 350); // client (150,150)

            AreaService.PointInRectangleEitherSpace(screenPoint, rect, windowTopLeft).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_ForScreenSpacePointInClientSpaceXpBarRect()
        {
            var svc = new AreaService();

            // Full screen rectangle expressed in screen-space LTRB.
            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", new RectangleF(100, 200, 900, 800));
            PrivateFieldAccessor.Set(svc, "_healthSquareRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_flaskRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_flaskTertiaryRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_skillsRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_skillsTertiaryRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_manaSquareRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_buffsAndDebuffsRectangle", RectangleF.Empty);

            // XP bar rectangle expressed in client-space LTRB.
            PrivateFieldAccessor.Set(svc, "_xpBarBlockedRectangle", new RectangleF(120, 120, 220, 200));

            var screenPointInsideXpBar = new Vector2(250, 350); // client (150,150)
            svc.PointIsInClickableArea(screenPointInsideXpBar).Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInXpBarBlockedArea_ReturnsTrue_ForPointInXpBarRect()
        {
            var svc = new AreaService();

            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", new RectangleF(100, 200, 900, 800));
            PrivateFieldAccessor.Set(svc, "_xpBarBlockedRectangle", new RectangleF(120, 120, 220, 200));

            var point = new Vector2(250, 350); // client (150,150)
            svc.PointIsInXpBarBlockedArea(gameController: null, point, forceRefresh: false).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInXpBarBlockedArea_ReturnsFalse_ForPointOutsideXpBarRect()
        {
            var svc = new AreaService();

            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", new RectangleF(100, 200, 900, 800));
            PrivateFieldAccessor.Set(svc, "_xpBarBlockedRectangle", new RectangleF(120, 120, 220, 200));

            var point = new Vector2(330, 420); // client (230,220)
            svc.PointIsInXpBarBlockedArea(gameController: null, point, forceRefresh: false).Should().BeFalse();
        }

        [TestMethod]
        public void PointIsInXpBarBlockedArea_ReturnsTrue_ForBottomFallbackBand_WhenXpRectMissing()
        {
            var svc = new AreaService();

            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", new RectangleF(100, 200, 900, 800));
            PrivateFieldAccessor.Set(svc, "_xpBarBlockedRectangle", RectangleF.Empty);

            var pointNearBottom = new Vector2(500, 790);
            svc.PointIsInXpBarBlockedArea(gameController: null, pointNearBottom, forceRefresh: false).Should().BeTrue();
        }

        [TestMethod]
        public void ResolveWorldItemXpFallbackBand_ReturnsBottomStripOfFullScreen()
        {
            RectangleF fullScreen = new RectangleF(100, 200, 900, 800);

            RectangleF band = AreaService.ResolveWorldItemXpFallbackBand(fullScreen, 42f);

            band.X.Should().Be(100);
            band.Y.Should().Be(758);
            band.Width.Should().Be(900);
            band.Height.Should().Be(800);
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InAltarBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var altarBlocked = new RectangleF(250, 120, 320, 190);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_altarBlockedRectangle", altarBlocked);

            svc.PointIsInClickableArea(new Vector2(280, 150)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(230, 150)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InRitualBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var ritualBlocked = new RectangleF(260, 130, 330, 210);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_ritualBlockedRectangle", ritualBlocked);

            svc.PointIsInClickableArea(new Vector2(300, 170)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(230, 170)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InTertiaryFlaskAndSkillsRectangles()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 500, 300);
            var health = new RectangleF(0, 290, 500, 300);
            var mana = new RectangleF(490, 290, 500, 300);
            var buffs = new RectangleF(0, 0, 30, 30);

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_flaskTertiaryRectangle", new RectangleF(120, 220, 170, 300));
            PrivateFieldAccessor.Set(svc, "_skillsTertiaryRectangle", new RectangleF(330, 220, 380, 300));

            svc.PointIsInClickableArea(new Vector2(140, 250)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(350, 250)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(250, 200)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InBuffsAndDebuffsBlockedRectangles()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = RectangleF.Empty;
            var buffRects = new List<RectangleF>
            {
                new RectangleF(20, 20, 80, 80),
                new RectangleF(90, 20, 150, 80)
            };

            SetRectangles(svc, full, health, mana, buffs);
            PrivateFieldAccessor.Set(svc, "_buffsAndDebuffsRectangles", buffRects);

            svc.PointIsInClickableArea(new Vector2(50, 50)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(120, 50)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(200, 50)).Should().BeTrue();
        }

        private static void SetRectangles(AreaService svc, RectangleF full, RectangleF health, RectangleF mana, RectangleF buffs)
        {
            PrivateFieldAccessor.Set(svc, "_fullScreenRectangle", full);
            PrivateFieldAccessor.Set(svc, "_healthAndFlaskRectangle", health);
            PrivateFieldAccessor.Set(svc, "_manaAndSkillsRectangle", mana);
            PrivateFieldAccessor.Set(svc, "_healthSquareRectangle", health);
            PrivateFieldAccessor.Set(svc, "_flaskRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_flaskTertiaryRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_skillsRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_skillsTertiaryRectangle", RectangleF.Empty);
            PrivateFieldAccessor.Set(svc, "_manaSquareRectangle", mana);
            PrivateFieldAccessor.Set(svc, "_buffsAndDebuffsRectangle", buffs);
        }
    }
}
