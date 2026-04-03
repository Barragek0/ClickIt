using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Features.Area;
using SharpDX;
using System.Collections.Generic;

namespace ClickIt.Tests.Features.Area
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

            ApplySnapshot(svc, full, health, mana, buffs);

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

            ApplySnapshot(svc, full, health, mana, buffs);

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

            ApplySnapshot(svc, full, health, mana, buffs);

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

            ApplySnapshot(svc, full, health, mana, buffs);

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
                new RectangleF(300, 50, 20, 60),
                new RectangleF(330, 50, 20, 60)
            };

            ApplySnapshot(svc, full, health, mana, buffs, questTrackerBlockedRectangles: questTrackerBlocks);

            svc.PointIsInClickableArea(new Vector2(310, 80)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(340, 80)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(260, 80)).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRetainQuestTrackerRectanglesOnEmptyRead_ReturnsTrue_WithinHoldWindow()
        {
            bool retain = BlockedAreaRefreshScheduler.ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                currentRectangleCount: 2,
                now: 1_100,
                lastSuccessTimestampMs: 1_000,
                holdLastGoodMs: 200);

            retain.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRetainQuestTrackerRectanglesOnEmptyRead_ReturnsFalse_AfterHoldWindow()
        {
            bool retain = BlockedAreaRefreshScheduler.ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                currentRectangleCount: 2,
                now: 1_250,
                lastSuccessTimestampMs: 1_000,
                holdLastGoodMs: 200);

            retain.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsFalse_InsideRefreshWindow()
        {
            bool shouldRefresh = BlockedAreaRefreshScheduler.ShouldRefresh(
                now: 10_500,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 10_000);

            shouldRefresh.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsTrue_WhenIntervalElapsed()
        {
            bool shouldRefresh = BlockedAreaRefreshScheduler.ShouldRefresh(
                now: 20_000,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 10_000);

            shouldRefresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsTrue_OnFirstRefresh()
        {
            bool shouldRefresh = BlockedAreaRefreshScheduler.ShouldRefresh(
                now: 10_500,
                lastRefreshTimestampMs: 0,
                refreshIntervalMs: 10_000);

            shouldRefresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_ReturnsTrue_WhenForceRefreshRequested()
        {
            bool shouldRefresh = BlockedAreaRefreshScheduler.ShouldRefresh(
                now: 10_500,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 10_000,
                forceRefresh: true);

            shouldRefresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshBlockedUiRectangles_Supports500MsWindow_ForBuffsAndDebuffsRefresh()
        {
            bool shouldRefreshTooSoon = BlockedAreaRefreshScheduler.ShouldRefresh(
                now: 10_300,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 500);

            bool shouldRefreshOnTime = BlockedAreaRefreshScheduler.ShouldRefresh(
                now: 10_500,
                lastRefreshTimestampMs: 10_000,
                refreshIntervalMs: 500);

            shouldRefreshTooSoon.Should().BeFalse();
            shouldRefreshOnTime.Should().BeTrue();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsTrue_OnFirstKnownArea()
        {
            bool changed = AreaChangeRules.HasAreaHashChanged(
                currentAreaHash: 123,
                lastKnownAreaHash: long.MinValue);

            changed.Should().BeTrue();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsTrue_WhenAreaChanges()
        {
            bool changed = AreaChangeRules.HasAreaHashChanged(
                currentAreaHash: 124,
                lastKnownAreaHash: 123);

            changed.Should().BeTrue();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsFalse_WhenAreaStaysSame()
        {
            bool changed = AreaChangeRules.HasAreaHashChanged(
                currentAreaHash: 123,
                lastKnownAreaHash: 123);

            changed.Should().BeFalse();
        }

        [TestMethod]
        public void HasAreaHashChanged_ReturnsFalse_WhenCurrentHashUnknown()
        {
            bool changed = AreaChangeRules.HasAreaHashChanged(
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
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: true).Should().BeTrue();
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: false).Should().BeFalse();
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: false, elementIsVisible: true).Should().BeFalse();
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: false, elementIsVisible: false).Should().BeFalse();
        }

        [TestMethod]
        public void AltarBlockedRectangle_VisibilityRule_RequiresVisibleElement()
        {
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: false).Should().BeFalse();
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: true).Should().BeTrue();
        }

        [TestMethod]
        public void MirageBlockedRectangle_VisibilityRule_RequiresVisibleElement()
        {
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: false).Should().BeFalse();
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: true).Should().BeTrue();
        }

        [TestMethod]
        public void RitualBlockedRectangle_VisibilityRule_RequiresVisibleElement()
        {
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: false).Should().BeFalse();
            AreaVisibilityRules.ShouldUseVisibleUiBlockedRectangle(elementIsValid: true, elementIsVisible: true).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InChatPanelBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var chatBlocked = new RectangleF(40, 220, 100, 40);

            ApplySnapshot(svc, full, health, mana, buffs, chatPanelBlockedRectangle: chatBlocked);

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
            var mapBlocked = new RectangleF(300, 0, 90, 120);

            ApplySnapshot(svc, full, health, mana, buffs, mapPanelBlockedRectangle: mapBlocked);

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
            var xpBarBlocked = new RectangleF(120, 120, 70, 80);

            ApplySnapshot(svc, full, health, mana, buffs, xpBarBlockedRectangle: xpBarBlocked);

            svc.PointIsInClickableArea(new Vector2(180, 150)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(230, 150)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InAltarBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var altarBlocked = new RectangleF(250, 120, 60, 70);

            ApplySnapshot(svc, full, health, mana, buffs, altarBlockedRectangle: altarBlocked);

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
            var ritualBlocked = new RectangleF(260, 130, 60, 80);

            ApplySnapshot(svc, full, health, mana, buffs, ritualBlockedRectangle: ritualBlocked);

            svc.PointIsInClickableArea(new Vector2(300, 170)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(230, 170)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InSentinelBlockedRectangle()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 400, 300);
            var health = new RectangleF(0, 290, 400, 300);
            var mana = new RectangleF(390, 290, 400, 300);
            var buffs = new RectangleF(0, 0, 30, 30);
            var sentinelBlocked = new RectangleF(200, 120, 70, 80);

            ApplySnapshot(svc, full, health, mana, buffs, sentinelBlockedRectangle: sentinelBlocked);

            svc.PointIsInClickableArea(new Vector2(230, 150)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(300, 150)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_InTertiaryFlaskAndSkillsRectangles()
        {
            var svc = new AreaService();

            var full = new RectangleF(0, 0, 500, 300);
            var health = new RectangleF(0, 290, 500, 300);
            var mana = new RectangleF(490, 290, 500, 300);
            var buffs = new RectangleF(0, 0, 30, 30);

            ApplySnapshot(
                svc,
                full,
                health,
                mana,
                buffs,
                flaskTertiaryRectangle: new RectangleF(120, 220, 170, 300),
                skillsTertiaryRectangle: new RectangleF(330, 220, 380, 300));

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
                new RectangleF(20, 20, 60, 60),
                new RectangleF(90, 20, 60, 60)
            };

            ApplySnapshot(svc, full, health, mana, buffs, buffsAndDebuffsRectangles: buffRects);

            svc.PointIsInClickableArea(new Vector2(50, 50)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(120, 50)).Should().BeFalse();
            svc.PointIsInClickableArea(new Vector2(200, 50)).Should().BeTrue();
        }

        [TestMethod]
        public void PointIsInClickableArea_ReturnsFalse_ForUiBlockedRectangles_WhenPointIsAbsoluteAndRectIsClientSpace()
        {
            var svc = new AreaService();

            // Full-screen in absolute coordinates (window offset from origin).
            var full = new RectangleF(100, 50, 900, 650);
            var health = new RectangleF(100, 600, 200, 650);
            var mana = new RectangleF(800, 600, 900, 650);
            var buffs = RectangleF.Empty;

            ApplySnapshot(
                svc,
                full,
                health,
                mana,
                buffs,
                altarBlockedRectangle: new RectangleF(200, 100, 120, 60));

            // Absolute point maps to client (220,120) and should be blocked.
            svc.PointIsInClickableArea(new Vector2(320, 170)).Should().BeFalse();

            // Absolute point maps to client (500,400) and should remain clickable.
            svc.PointIsInClickableArea(new Vector2(600, 450)).Should().BeTrue();
        }

        private static void ApplySnapshot(
            AreaService svc,
            RectangleF full,
            RectangleF health,
            RectangleF mana,
            RectangleF buffs,
            RectangleF? flaskTertiaryRectangle = null,
            RectangleF? skillsTertiaryRectangle = null,
            RectangleF? chatPanelBlockedRectangle = null,
            RectangleF? mapPanelBlockedRectangle = null,
            RectangleF? xpBarBlockedRectangle = null,
            RectangleF? altarBlockedRectangle = null,
            RectangleF? ritualBlockedRectangle = null,
            RectangleF? sentinelBlockedRectangle = null,
            IReadOnlyList<RectangleF>? buffsAndDebuffsRectangles = null,
            IReadOnlyList<RectangleF>? questTrackerBlockedRectangles = null)
        {
            svc.ApplyBlockedSnapshot(new AreaBlockedSnapshot
            {
                FullScreenRectangle = full,
                HealthAndFlaskRectangle = health,
                ManaAndSkillsRectangle = mana,
                HealthSquareRectangle = health,
                FlaskRectangle = RectangleF.Empty,
                FlaskTertiaryRectangle = flaskTertiaryRectangle ?? RectangleF.Empty,
                SkillsRectangle = RectangleF.Empty,
                SkillsTertiaryRectangle = skillsTertiaryRectangle ?? RectangleF.Empty,
                ManaSquareRectangle = mana,
                BuffsAndDebuffsRectangle = buffs,
                ChatPanelBlockedRectangle = chatPanelBlockedRectangle ?? RectangleF.Empty,
                MapPanelBlockedRectangle = mapPanelBlockedRectangle ?? RectangleF.Empty,
                XpBarBlockedRectangle = xpBarBlockedRectangle ?? RectangleF.Empty,
                AltarBlockedRectangle = altarBlockedRectangle ?? RectangleF.Empty,
                RitualBlockedRectangle = ritualBlockedRectangle ?? RectangleF.Empty,
                SentinelBlockedRectangle = sentinelBlockedRectangle ?? RectangleF.Empty,
                BuffsAndDebuffsRectangles = buffsAndDebuffsRectangles ?? [],
                QuestTrackerBlockedRectangles = questTrackerBlockedRectangles ?? []
            });
        }
    }
}
