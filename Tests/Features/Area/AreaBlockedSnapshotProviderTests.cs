namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class AreaBlockedSnapshotProviderTests
    {
        [TestMethod]
        public void ApplySnapshot_ReplacesExistingBlockedRectangleCollections()
        {
            var provider = new AreaBlockedSnapshotProvider();

            provider.ApplySnapshot(new AreaBlockedSnapshot
            {
                BuffsAndDebuffsRectangles = [new RectangleF(1, 1, 10, 10)],
                QuestTrackerBlockedRectangles = [new RectangleF(2, 2, 10, 10)]
            });

            provider.ApplySnapshot(new AreaBlockedSnapshot());
            AreaBlockedSnapshot current = provider.CurrentSnapshot;

            current.BuffsAndDebuffsRectangles.Should().BeEmpty();
            current.QuestTrackerBlockedRectangles.Should().BeEmpty();
        }

        [TestMethod]
        public void ApplySnapshot_PublishesCopiedCollections()
        {
            var provider = new AreaBlockedSnapshotProvider();
            var buffs = new List<RectangleF> { new(10, 10, 20, 20) };
            var quest = new List<RectangleF> { new(30, 30, 40, 40) };

            provider.ApplySnapshot(new AreaBlockedSnapshot
            {
                BuffsAndDebuffsRectangles = buffs,
                QuestTrackerBlockedRectangles = quest
            });

            buffs.Clear();
            quest.Clear();
            AreaBlockedSnapshot current = provider.CurrentSnapshot;

            current.BuffsAndDebuffsRectangles.Should().ContainSingle();
            current.QuestTrackerBlockedRectangles.Should().ContainSingle();
        }

        [TestMethod]
        public void ApplySnapshot_PublishesRefreshTimestamps()
        {
            var provider = new AreaBlockedSnapshotProvider();

            provider.ApplySnapshot(new AreaBlockedSnapshot
            {
                LastBlockedUiRectanglesRefreshTimestampMs = 123,
                LastBuffsAndDebuffsRectanglesRefreshTimestampMs = 456
            });

            AreaBlockedSnapshot current = provider.CurrentSnapshot;

            current.LastBlockedUiRectanglesRefreshTimestampMs.Should().Be(123);
            current.LastBuffsAndDebuffsRectanglesRefreshTimestampMs.Should().Be(456);
        }

        [TestMethod]
        public void ApplyQuestTrackerRectangles_RetainsExistingRectangles_OnEmptyReadWithinHoldWindow()
        {
            var blockedState = new AreaBlockedState
            {
                LastQuestTrackerRectanglesSuccessTimestampMs = 1_000
            };
            blockedState.QuestTrackerBlockedRectangles.Add(new RectangleF(10, 10, 20, 20));

            AreaBlockedSnapshotProvider.ApplyQuestTrackerRectangles(blockedState, [], now: 1_500);

            blockedState.QuestTrackerBlockedRectangles.Should().ContainSingle();
            blockedState.LastQuestTrackerRectanglesSuccessTimestampMs.Should().Be(1_000);
        }

        [TestMethod]
        public void ApplyQuestTrackerRectangles_ClearsExistingRectangles_OnEmptyReadAfterHoldWindow()
        {
            var blockedState = new AreaBlockedState
            {
                LastQuestTrackerRectanglesSuccessTimestampMs = 1_000
            };
            blockedState.QuestTrackerBlockedRectangles.Add(new RectangleF(10, 10, 20, 20));

            AreaBlockedSnapshotProvider.ApplyQuestTrackerRectangles(blockedState, [], now: 2_500);

            blockedState.QuestTrackerBlockedRectangles.Should().BeEmpty();
            blockedState.LastQuestTrackerRectanglesSuccessTimestampMs.Should().Be(1_000);
        }

        [TestMethod]
        public void ApplyQuestTrackerRectangles_ReplacesRectangles_AndUpdatesTimestamp_OnSuccessfulRead()
        {
            var blockedState = new AreaBlockedState
            {
                LastQuestTrackerRectanglesSuccessTimestampMs = 1_000
            };
            blockedState.QuestTrackerBlockedRectangles.Add(new RectangleF(10, 10, 20, 20));

            AreaBlockedSnapshotProvider.ApplyQuestTrackerRectangles(
                blockedState,
                [new RectangleF(30, 30, 40, 40), new RectangleF(50, 50, 60, 60)],
                now: 2_000);

            blockedState.QuestTrackerBlockedRectangles.Should().HaveCount(2);
            blockedState.QuestTrackerBlockedRectangles[0].Should().Be(new RectangleF(30, 30, 40, 40));
            blockedState.QuestTrackerBlockedRectangles[1].Should().Be(new RectangleF(50, 50, 60, 60));
            blockedState.LastQuestTrackerRectanglesSuccessTimestampMs.Should().Be(2_000);
        }

        [TestMethod]
        public void ApplyBuffAndDebuffRectangles_StoresAllRectangles_AndUsesFirstAsAggregate()
        {
            var blockedState = new AreaBlockedState();

            AreaBlockedSnapshotProvider.ApplyBuffAndDebuffRectangles(
                blockedState,
                [new RectangleF(1, 2, 3, 4), new RectangleF(5, 6, 7, 8)]);

            blockedState.BuffsAndDebuffsRectangles.Should().HaveCount(2);
            blockedState.BuffsAndDebuffsRectangles[0].Should().Be(new RectangleF(1, 2, 3, 4));
            blockedState.BuffsAndDebuffsRectangles[1].Should().Be(new RectangleF(5, 6, 7, 8));
            blockedState.BuffsAndDebuffsRectangle.Should().Be(new RectangleF(1, 2, 3, 4));
        }

        [TestMethod]
        public void ApplyBuffAndDebuffRectangles_ClearsAggregateRectangle_WhenInputIsEmpty()
        {
            var blockedState = new AreaBlockedState();
            blockedState.BuffsAndDebuffsRectangles.Add(new RectangleF(1, 2, 3, 4));
            blockedState.BuffsAndDebuffsRectangle = new RectangleF(1, 2, 3, 4);

            AreaBlockedSnapshotProvider.ApplyBuffAndDebuffRectangles(blockedState, []);

            blockedState.BuffsAndDebuffsRectangles.Should().BeEmpty();
            blockedState.BuffsAndDebuffsRectangle.Should().Be(RectangleF.Empty);
        }

        [TestMethod]
        public void ShouldUpdateMapPanelBlockedRectangle_ReturnsFalse_OnlyInTownOrHideout()
        {
            AreaBlockedSnapshotProvider.ShouldUpdateMapPanelBlockedRectangle(isInTownOrHideout: false).Should().BeTrue();
            AreaBlockedSnapshotProvider.ShouldUpdateMapPanelBlockedRectangle(isInTownOrHideout: true).Should().BeFalse();
        }

        [TestMethod]
        public void SplitBottomAnchoredRectangleFromLeft_ReturnsExpectedPrimaryAndSecondaryRectangles()
        {
            (RectangleF primary, RectangleF secondary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromLeft(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f);

            AssertRectangle(primary, 0, 0, 40, 40);
            AssertRectangle(secondary, 40, 15, 73.3f, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredRectangleFromRight_ReturnsExpectedPrimaryAndSecondaryRectangles()
        {
            (RectangleF primary, RectangleF secondary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromRight(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f);

            AssertRectangle(primary, 60, 0, 100, 40);
            AssertRectangle(secondary, 26.7f, 15, 60, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredThreeRectanglesFromLeft_ReturnsExpectedLinkedTertiaryRectangle()
        {
            (RectangleF primary, RectangleF secondary, RectangleF tertiary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromLeft(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f,
                tertiaryHeightRatio: 0.85f,
                tertiaryWidthRatio: 0.79f);

            AssertRectangle(primary, 0, 0, 40, 40);
            AssertRectangle(secondary, 40, 15, 73.3f, 40);
            AssertRectangle(tertiary, 73.3f, 18.75f, 99.607f, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredThreeRectanglesFromRight_ReturnsExpectedLinkedTertiaryRectangle()
        {
            (RectangleF primary, RectangleF secondary, RectangleF tertiary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromRight(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f,
                tertiaryHeightRatio: 0.85f,
                tertiaryWidthRatio: 0.79f);

            AssertRectangle(primary, 60, 0, 100, 40);
            AssertRectangle(secondary, 26.7f, 15, 60, 40);
            AssertRectangle(tertiary, 0.3930006f, 18.75f, 26.7f, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredRectangleHelpers_ReturnEmpty_WhenSourceHasNoArea()
        {
            AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromLeft(RectangleF.Empty, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty));

            AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromRight(RectangleF.Empty, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty));

            AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromLeft(RectangleF.Empty, 0.5f, 0.5f, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty, RectangleF.Empty));

            AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromRight(RectangleF.Empty, 0.5f, 0.5f, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty, RectangleF.Empty));
        }

        [TestMethod]
        public void UpdateScreenAreas_SkipsRefresh_WhenAreaIsUnchanged_AndIntervalsHaveNotElapsed()
        {
            var provider = new AreaBlockedSnapshotProvider();
            long now = Environment.TickCount64;
            RectangleF existingChatRect = new(40, 220, 100, 40);
            RectangleF existingBuffRect = new(1, 2, 3, 4);
            RectangleF existingQuestRect = new(5, 6, 7, 8);
            provider.ApplySnapshot(new AreaBlockedSnapshot
            {
                LastBlockedUiRectanglesRefreshTimestampMs = now,
                LastBuffsAndDebuffsRectanglesRefreshTimestampMs = now,
                ChatPanelBlockedRectangle = existingChatRect,
                BuffsAndDebuffsRectangle = existingBuffRect,
                BuffsAndDebuffsRectangles = [existingBuffRect],
                QuestTrackerBlockedRectangles = [existingQuestRect]
            });
            GetBlockedState(provider).LastKnownAreaHash = 789;
            GameController gameController = CreateAreaGameController(new RectangleF(100, 200, 1200, 800), currentAreaHash: 789);

            provider.UpdateScreenAreas(gameController, blockedUiRefreshIntervalMs: 10_000, forceBlockedUiRefresh: false);

            AreaBlockedSnapshot snapshot = provider.CurrentSnapshot;
            snapshot.LastBlockedUiRectanglesRefreshTimestampMs.Should().Be(now);
            snapshot.LastBuffsAndDebuffsRectanglesRefreshTimestampMs.Should().Be(now);
            snapshot.ChatPanelBlockedRectangle.Should().Be(existingChatRect);
            snapshot.BuffsAndDebuffsRectangles.Should().Equal([existingBuffRect]);
            snapshot.QuestTrackerBlockedRectangles.Should().Equal([existingQuestRect]);
        }

        [TestMethod]
        public void HasAreaChanged_ReturnsFalse_WhenCurrentAreaHashMatchesStoredHash()
        {
            var provider = new AreaBlockedSnapshotProvider();
            GetBlockedState(provider).LastKnownAreaHash = 123;
            GameController gameController = CreateAreaGameController(RectangleF.Empty, currentAreaHash: 123);

            bool changed = InvokeHasAreaChanged(provider, gameController);

            changed.Should().BeFalse();
            GetBlockedState(provider).LastKnownAreaHash.Should().Be(123);
        }

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, true)]
        [DataRow(false, true, true)]
        public void IsInTownOrHideout_ReturnsExpected_ForAreaFlags(bool isTown, bool isHideout, bool expected)
        {
            GameController gameController = CreateAreaGameController(RectangleF.Empty, currentAreaHash: 123, isTown: isTown, isHideout: isHideout);

            InvokeIsInTownOrHideout(gameController).Should().Be(expected);
        }

        [TestMethod]
        public void IsInTownOrHideout_ReturnsFalse_WhenControllerIsNull()
        {
            InvokeIsInTownOrHideout(null).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveMainUiRegions_UsesClientCoordinates_ForMainAndFlaskRegions()
        {
            RectangleF windowRect = new(100, 200, 1200, 800);
            (RectangleF fullScreen, RectangleF leftCombined, RectangleF rightCombined) = AreaBlockedSnapshotProvider.ResolveMainUiRegions(windowRect);

            fullScreen.Should().Be(new RectangleF(0f, 0f, windowRect.Width, windowRect.Height));

            RectangleF expectedLeftCombined = new(
                0f,
                windowRect.Height / 5f * 3.92f,
                windowRect.Width / 3.4f,
                windowRect.Height);
            RectangleF expectedRightCombined = new(
                windowRect.Width / 3f * 2.12f,
                windowRect.Height / 5f * 3.92f,
                windowRect.Width,
                windowRect.Height);

            leftCombined.Should().Be(expectedLeftCombined);
            rightCombined.Should().Be(expectedRightCombined);
        }

        [DataTestMethod]
        [DataRow(800f, 600f)]
        [DataRow(1280f, 720f)]
        [DataRow(1920f, 1080f)]
        [DataRow(2560f, 1440f)]
        [DataRow(3440f, 1440f)]
        public void ResolveMainUiRegions_ScalesAcrossClientResolutions_AndStaysWithinFullscreen(float width, float height)
        {
            RectangleF windowRect = new(333f, 777f, width, height);
            (RectangleF fullScreen, RectangleF leftCombined, RectangleF rightCombined) = AreaBlockedSnapshotProvider.ResolveMainUiRegions(windowRect);

            fullScreen.Should().Be(new RectangleF(0f, 0f, width, height));

            leftCombined.X.Should().BeGreaterOrEqualTo(0f);
            leftCombined.Y.Should().BeGreaterOrEqualTo(0f);
            leftCombined.Width.Should().BeLessOrEqualTo(width);
            leftCombined.Height.Should().BeLessOrEqualTo(height);

            rightCombined.X.Should().BeGreaterOrEqualTo(0f);
            rightCombined.Y.Should().BeGreaterOrEqualTo(0f);
            rightCombined.Width.Should().BeLessOrEqualTo(width);
            rightCombined.Height.Should().BeLessOrEqualTo(height);

            leftCombined.Height.Should().BeApproximately(height, 0.001f);
            rightCombined.Height.Should().BeApproximately(height, 0.001f);
            rightCombined.Width.Should().BeApproximately(width, 0.001f);
        }

        [DataTestMethod]
        [DataRow(1280f, 720f)]
        [DataRow(1920f, 1080f)]
        [DataRow(2560f, 1440f)]
        public void ResolveMainUiRegions_IsInvariantToWindowTopLeftOffset(float width, float height)
        {
            RectangleF rectNearOrigin = new(2f, 35f, width, height);
            RectangleF rectFarFromOrigin = new(649f, 401f, width, height);

            (RectangleF fullA, RectangleF leftA, RectangleF rightA) = AreaBlockedSnapshotProvider.ResolveMainUiRegions(rectNearOrigin);
            (RectangleF fullB, RectangleF leftB, RectangleF rightB) = AreaBlockedSnapshotProvider.ResolveMainUiRegions(rectFarFromOrigin);

            fullA.Should().Be(fullB);
            leftA.Should().Be(leftB);
            rightA.Should().Be(rightB);
        }

        private static void AssertRectangle(RectangleF actual, float x, float y, float width, float height)
        {
            actual.X.Should().BeApproximately(x, 0.0001f);
            actual.Y.Should().BeApproximately(y, 0.0001f);
            actual.Width.Should().BeApproximately(width, 0.0001f);
            actual.Height.Should().BeApproximately(height, 0.0001f);
        }

        private static AreaBlockedState GetBlockedState(AreaBlockedSnapshotProvider provider)
        {
            FieldInfo field = typeof(AreaBlockedSnapshotProvider).GetField("_blockedState", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Unable to locate _blockedState.");

            return (AreaBlockedState)field.GetValue(provider)!;
        }

        private static bool InvokeHasAreaChanged(AreaBlockedSnapshotProvider provider, GameController gameController)
        {
            MethodInfo method = typeof(AreaBlockedSnapshotProvider).GetMethod("HasAreaChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Unable to locate HasAreaChanged.");

            return (bool)method.Invoke(provider, [gameController])!;
        }

        private static bool InvokeIsInTownOrHideout(GameController? gameController)
        {
            MethodInfo method = typeof(AreaBlockedSnapshotProvider).GetMethod("IsInTownOrHideout", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Unable to locate IsInTownOrHideout.");

            return (bool)method.Invoke(null, [gameController])!;
        }

        private static GameController CreateAreaGameController(
            RectangleF windowRect,
            long currentAreaHash,
            bool isTown = false,
            bool isHideout = false,
            object? questTrackerRoot = null,
            object? chatPanelRoot = null,
            object? mapRoot = null,
            object? gameUiRoot = null)
        {
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(windowRect);
            FakeGameShim game = (FakeGameShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameShim));
            game.CurrentAreaHash = currentAreaHash;
            game.IngameState = new FakeIngameStateShim
            {
                IngameUi = new AreaUiSnapshotReaderTests.FakeIngameUi
                {
                    QuestTracker = questTrackerRoot,
                    ChatPanel = chatPanelRoot,
                    Map = mapRoot,
                    GameUI = gameUiRoot
                }
            };
            RuntimeMemberAccessor.SetRequiredMember(gameController, nameof(GameController.Game), game);

            Type areaType = RuntimeMemberAccessor.ResolveRequiredMemberType(gameController, nameof(GameController.Area));
            object area = RuntimeHelpers.GetUninitializedObject(areaType);
            Type currentAreaType = RuntimeMemberAccessor.ResolveRequiredMemberType(area, "CurrentArea");
            object currentArea = RuntimeHelpers.GetUninitializedObject(currentAreaType);
            RuntimeMemberAccessor.SetRequiredMember(currentArea, "IsTown", isTown);
            RuntimeMemberAccessor.SetRequiredMember(currentArea, "IsHideout", isHideout);
            RuntimeMemberAccessor.SetRequiredMember(area, "CurrentArea", currentArea);
            RuntimeMemberAccessor.SetRequiredMember(gameController, nameof(GameController.Area), area);
            return gameController;
        }

        private sealed class FakeGameShim : TheGame
        {
            public FakeGameShim()
                : base(null!, null!, null!, null!)
            {
            }

            public new object? CurrentAreaHash { get; set; }

            public new object? IngameState { get; set; }
        }

        private sealed class FakeIngameStateShim
        {
            public object? IngameUi { get; set; }
        }
    }
}