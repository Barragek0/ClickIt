namespace ClickIt.Tests.Shared.Input
{
    [TestClass]
    public class InputHandlerCanClickTests
    {
        [DataTestMethod]
        [DataRow(true, false, false, false, false, false, false, false, false, false, false, false, false, false, "Chat is open.")]
        [DataRow(false, true, false, false, false, false, false, false, false, false, false, false, false, false, "Atlas panel is open.")]
        [DataRow(false, false, true, false, false, false, false, false, false, false, false, false, false, false, "Atlas tree panel is open.")]
        [DataRow(false, false, false, true, false, false, false, false, false, false, false, false, false, false, "Passive tree panel is open.")]
        [DataRow(false, false, false, false, true, false, false, false, false, false, false, false, false, false, "Ultimatum panel is open (Click Ultimatum Choices is disabled).")]
        [DataRow(false, false, false, false, true, true, false, false, false, false, false, false, false, false, null)]
        [DataRow(false, false, false, false, false, false, true, false, false, false, false, false, false, false, "Syndicate panel is open.")]
        [DataRow(false, false, false, false, false, false, false, true, false, false, false, false, false, false, "Incursion window is open.")]
        [DataRow(false, false, false, false, false, false, false, false, true, false, false, false, false, false, "Ritual window is open.")]
        [DataRow(false, false, false, false, false, false, false, false, false, true, false, false, false, false, "Sanctum floor window is open.")]
        [DataRow(false, false, false, false, false, false, false, false, false, false, true, false, false, false, "Sanctum reward window is open.")]
        [DataRow(false, false, false, false, false, false, false, false, false, false, false, true, false, false, "Microtransaction shop window is open.")]
        [DataRow(false, false, false, false, false, false, false, false, false, false, false, false, true, false, "Resurrect panel is open.")]
        [DataRow(false, false, false, false, false, false, false, false, false, false, false, false, false, true, "NPC dialog is open.")]
        public void ResolveUiBlockingReason_ReturnsExpectedReasonInPriorityOrder(
            bool chatOpen,
            bool atlasPanelOpen,
            bool atlasTreePanelOpen,
            bool passiveTreePanelOpen,
            bool ultimatumPanelOpen,
            bool otherUltimatumClickEnabled,
            bool syndicatePanelOpen,
            bool incursionWindowOpen,
            bool ritualWindowOpen,
            bool sanctumFloorWindowOpen,
            bool sanctumRewardWindowOpen,
            bool microtransactionShopWindowOpen,
            bool resurrectPanelOpen,
            bool npcDialogOpen,
            string? expected)
        {
            var state = new UiBlockingState(
                ChatOpen: chatOpen,
                AtlasPanelOpen: atlasPanelOpen,
                AtlasTreePanelOpen: atlasTreePanelOpen,
                PassiveTreePanelOpen: passiveTreePanelOpen,
                UltimatumPanelOpen: ultimatumPanelOpen,
                SyndicatePanelOpen: syndicatePanelOpen,
                IncursionWindowOpen: incursionWindowOpen,
                RitualWindowOpen: ritualWindowOpen,
                SanctumFloorWindowOpen: sanctumFloorWindowOpen,
                SanctumRewardWindowOpen: sanctumRewardWindowOpen,
                MicrotransactionShopWindowOpen: microtransactionShopWindowOpen,
                ResurrectPanelOpen: resurrectPanelOpen,
                NpcDialogOpen: npcDialogOpen);

            string? reason = InputHandler.ResolveUiBlockingReason(state, otherUltimatumClickEnabled);

            reason.Should().Be(expected);
        }

        [TestMethod]
        public void CaptureUiBlockingState_ReadsVisibleElements_FromReflectionFriendlyUiState()
        {
            var uiState = new FakeUiState
            {
                ChatTitlePanel = new FakeVisibleElement { IsVisible = true },
                Atlas = new FakeVisibleElement { IsVisible = true },
                UltimatumPanel = new FakeVisibleElement { IsVisible = true },
                NpcDialog = new FakeVisibleElement { IsVisible = true }
            };

            UiBlockingState state = InputHandler.CaptureUiBlockingState(uiState);

            state.ChatOpen.Should().BeTrue();
            state.AtlasPanelOpen.Should().BeTrue();
            state.UltimatumPanelOpen.Should().BeTrue();
            state.NpcDialogOpen.Should().BeTrue();
            state.RitualWindowOpen.Should().BeFalse();
        }

        [TestMethod]
        public void CaptureUiBlockingState_UsesAlternatePropertyNames_ForAtlasAndBetrayalPanels()
        {
            var uiState = new AlternateNamedUiState
            {
                AtlasPanel = new FakeVisibleElement { IsVisible = true },
                BetrayalWindow = new FakeVisibleElement { IsVisible = true }
            };

            UiBlockingState state = InputHandler.CaptureUiBlockingState(uiState);

            state.AtlasPanelOpen.Should().BeTrue();
            state.SyndicatePanelOpen.Should().BeTrue();
            state.ChatOpen.Should().BeFalse();
        }

        [TestMethod]
        public void CaptureUiBlockingState_ReturnsFalse_WhenElementsAreInvisibleOrMissing()
        {
            var uiState = new FakeUiState
            {
                ChatTitlePanel = new FakeVisibleElement { IsVisible = false },
                RitualWindow = null
            };

            UiBlockingState state = InputHandler.CaptureUiBlockingState(uiState);

            state.ChatOpen.Should().BeFalse();
            state.RitualWindowOpen.Should().BeFalse();
            state.MicrotransactionShopWindowOpen.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, false, null, false, true)]
        [DataRow(false, false, false, false, false, null, false, false)]
        [DataRow(true, true, false, false, false, null, true, false)]
        [DataRow(true, false, true, false, false, null, false, false)]
        [DataRow(true, false, false, true, false, null, false, false)]
        [DataRow(true, false, false, false, true, null, false, false)]
        [DataRow(true, false, false, false, false, "Chat is open.", false, false)]
        public void ShouldAllowClickWithoutInputState_ReturnsExpectedResult(
            bool isPoeActive,
            bool isPanelOpen,
            bool isInTownOrHideout,
            bool isInToggleItemsPostClickBlockWindow,
            bool isEscapeState,
            string? uiBlockingReason,
            bool blockOnOpenPanels,
            bool expected)
        {
            bool allowed = InputHandler.ShouldAllowClickWithoutInputState(
                isPoeActive,
                isPanelOpen,
                isInTownOrHideout,
                isInToggleItemsPostClickBlockWindow,
                isEscapeState,
                uiBlockingReason,
                blockOnOpenPanels);

            allowed.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, false, false, false, false, null, false, "PoE not in focus.")]
        [DataRow(true, true, false, false, false, null, true, "Panel is open.")]
        [DataRow(true, false, true, false, false, null, false, "In town/hideout.")]
        [DataRow(true, false, false, true, false, null, false, "Waiting after Toggle Item View.")]
        [DataRow(true, false, false, false, false, "Chat is open.", false, "Chat is open.")]
        [DataRow(true, false, false, false, true, null, false, "Escape menu is open.")]
        [DataRow(true, false, false, false, false, null, false, "Clicking disabled.")]
        public void ResolveCanClickFailureReason_ReturnsExpectedReason(
            bool isPoeActive,
            bool isPanelOpen,
            bool isInTownOrHideout,
            bool isInToggleItemsPostClickBlockWindow,
            bool isEscapeState,
            string? uiBlockingReason,
            bool blockOnOpenPanels,
            string expected)
        {
            string reason = InputHandler.ResolveCanClickFailureReason(
                isPoeActive,
                isPanelOpen,
                isInTownOrHideout,
                isInToggleItemsPostClickBlockWindow,
                isEscapeState,
                uiBlockingReason,
                blockOnOpenPanels);

            reason.Should().Be(expected);
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenLazyModeDisabled_AndHotkeyInactive()
        {
            var settings = new ClickItSettings();
            var handler = new InputHandler(settings);

            bool pressed = handler.IsClickHotkeyPressed(cachedLabels: null, hasLazyModeRestrictedItemsOnScreen: null);

            pressed.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsTrue_WhenLazyModeEnabled_AndNoRestrictionsOrDisableState()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.DisableLazyModeLeftClickHeld.Value = false;
            settings.DisableLazyModeRightClickHeld.Value = false;

            var handler = new InputHandler(settings);
            List<LabelOnGround> labels = [ExileCoreVisibleObjectBuilder.CreateSelectableLabel()];
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => labels, 50);
            IReadOnlyList<LabelOnGround>? observedLabels = null;

            bool pressed = handler.IsClickHotkeyPressed(
                cachedLabels,
                hasLazyModeRestrictedItemsOnScreen: currentLabels =>
                {
                    observedLabels = currentLabels;
                    return false;
                });

            pressed.Should().BeTrue();
            observedLabels.Should().BeSameAs(labels);
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenLazyModeEnabled_AndRestrictedItemsPresent()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;

            var handler = new InputHandler(settings);
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);

            bool pressed = handler.IsClickHotkeyPressed(cachedLabels, hasLazyModeRestrictedItemsOnScreen: _ => true);

            pressed.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenLazyModeDisableHotkeyIsLatched()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.LazyModeDisableKeyToggleMode.Value = true;

            var handler = new InputHandler(settings);
            SeedHotkeyState(handler, lazyModeDisableToggled: true);

            bool pressed = handler.IsClickHotkeyPressed(
                new TimeCache<List<LabelOnGround>>(() => [], 50),
                hasLazyModeRestrictedItemsOnScreen: _ => false);

            pressed.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsTrue_WhenClickHotkeyIsLatched_EvenIfLazyModeWouldOtherwiseBlock()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickHotkeyToggleMode.Value = true;
            settings.LazyModeDisableKeyToggleMode.Value = true;

            var handler = new InputHandler(settings);
            SeedHotkeyState(handler, clickHotkeyToggled: true, lazyModeDisableToggled: true);

            bool pressed = handler.IsClickHotkeyPressed(
                new TimeCache<List<LabelOnGround>>(() => [], 50),
                hasLazyModeRestrictedItemsOnScreen: _ => true);

            pressed.Should().BeTrue();
        }

        [TestMethod]
        public void IsClickKeyStateActive_ReturnsFalse_WhenLazyModeDisabled_AndHotkeyInactive()
        {
            var handler = new InputHandler(new ClickItSettings());

            bool active = InvokeIsClickKeyStateActive(handler, hasLazyModeRestrictedItemsOnScreen: false);

            active.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickKeyStateActive_ReturnsTrue_WhenLazyModeEnabled_AndNoRestrictionsOrDisableState()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            var handler = new InputHandler(settings);

            bool active = InvokeIsClickKeyStateActive(handler, hasLazyModeRestrictedItemsOnScreen: false);

            active.Should().BeTrue();
        }

        [TestMethod]
        public void IsClickKeyStateActive_ReturnsFalse_WhenLazyModeDisableHotkeyIsLatched_AndHotkeyInactive()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.LazyModeDisableKeyToggleMode.Value = true;
            var handler = new InputHandler(settings);
            SeedHotkeyState(handler, lazyModeDisableToggled: true);

            bool active = InvokeIsClickKeyStateActive(handler, hasLazyModeRestrictedItemsOnScreen: false);

            active.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickKeyStateActive_ReturnsTrue_WhenHotkeyIsLatched_EvenIfLazyModeIsRestricted()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.ClickHotkeyToggleMode.Value = true;
            var handler = new InputHandler(settings);
            SeedHotkeyState(handler, clickHotkeyToggled: true);

            bool active = InvokeIsClickKeyStateActive(handler, hasLazyModeRestrictedItemsOnScreen: true);

            active.Should().BeTrue();
        }

        [TestMethod]
        public void CanClick_ReturnsFalse_WhenHotkeyInactive_AndGameControllerGraphIsPartial()
        {
            var handler = new InputHandler(new ClickItSettings());
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f));

            bool canClick = handler.CanClick(gameController);

            canClick.Should().BeFalse();
        }

        [TestMethod]
        public void GetCanClickFailureReason_ReturnsNotInFocus_WhenWindowForegroundLookupThrows_OnPartialGraph()
        {
            var handler = new InputHandler(new ClickItSettings());
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f));

            string reason = handler.GetCanClickFailureReason(gameController);

            reason.Should().Be("PoE not in focus.");
        }

        private static bool InvokeIsClickKeyStateActive(InputHandler handler, bool hasLazyModeRestrictedItemsOnScreen)
        {
            MethodInfo method = typeof(InputHandler).GetMethod("IsClickKeyStateActive", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (bool)method.Invoke(handler, [hasLazyModeRestrictedItemsOnScreen])!;
        }

        private static void SeedHotkeyState(
            InputHandler handler,
            bool? clickHotkeyToggled = null,
            bool? clickHotkeyWasDown = null,
            bool? lazyModeDisableToggled = null,
            bool? lazyModeDisableKeyWasDown = null)
        {
            object hotkeyStateService = RuntimeMemberAccessor.GetRequiredMemberValue(handler, "_hotkeyStateService")!;

            if (clickHotkeyToggled.HasValue)
                RuntimeMemberAccessor.SetRequiredMember(hotkeyStateService, "_clickHotkeyToggled", clickHotkeyToggled.Value);

            if (clickHotkeyWasDown.HasValue)
                RuntimeMemberAccessor.SetRequiredMember(hotkeyStateService, "_clickHotkeyWasDown", clickHotkeyWasDown.Value);

            if (lazyModeDisableToggled.HasValue)
                RuntimeMemberAccessor.SetRequiredMember(hotkeyStateService, "_lazyModeDisableToggled", lazyModeDisableToggled.Value);

            if (lazyModeDisableKeyWasDown.HasValue)
                RuntimeMemberAccessor.SetRequiredMember(hotkeyStateService, "_lazyModeDisableKeyWasDown", lazyModeDisableKeyWasDown.Value);
        }

        private sealed class FakeUiState
        {
            public FakeVisibleElement? ChatTitlePanel { get; init; }
            public FakeVisibleElement? Atlas { get; init; }
            public FakeVisibleElement? AtlasTreePanel { get; init; }
            public FakeVisibleElement? TreePanel { get; init; }
            public FakeVisibleElement? UltimatumPanel { get; init; }
            public FakeVisibleElement? SyndicatePanel { get; init; }
            public FakeVisibleElement? IncursionWindow { get; init; }
            public FakeVisibleElement? RitualWindow { get; init; }
            public FakeVisibleElement? SanctumFloorWindow { get; init; }
            public FakeVisibleElement? SanctumRewardWindow { get; init; }
            public FakeVisibleElement? MicrotransactionShopWindow { get; init; }
            public FakeVisibleElement? ResurrectPanel { get; init; }
            public FakeVisibleElement? NpcDialog { get; init; }
        }

        private sealed class AlternateNamedUiState
        {
            public FakeVisibleElement? AtlasPanel { get; init; }
            public FakeVisibleElement? BetrayalWindow { get; init; }
        }

        private sealed class FakeVisibleElement
        {
            public bool IsVisible { get; init; }
        }
    }
}
