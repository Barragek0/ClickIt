namespace ClickIt.Features.Click.Application
{
    internal readonly record struct AltarAutomationServiceDependencies(
        ClickItSettings Settings,
        GameController GameController,
        Func<IReadOnlyList<PrimaryAltarComponent>> GetAltarSnapshot,
        Action<Element> RemoveTrackedAltarByElement,
        Func<PrimaryAltarComponent, AltarWeights> CalculateAltarWeights,
        Func<PrimaryAltarComponent, AltarWeights, RectangleF, RectangleF, Vector2, Element?> DetermineAltarChoice,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Func<InteractionExecutionRequest, bool> ExecuteInteraction,
        Action<string> DebugLog,
        Action<string, int> LogError,
        object ElementAccessLock);

    internal sealed class AltarAutomationService(AltarAutomationServiceDependencies dependencies)
    {
        private readonly AltarAutomationServiceDependencies _dependencies = dependencies;

        public IEnumerator ProcessAltarClicking()
        {
            IReadOnlyList<PrimaryAltarComponent> altarSnapshot = _dependencies.GetAltarSnapshot();
            if (altarSnapshot.Count == 0)
                yield break;

            bool clickEater = _dependencies.Settings.ClickEaterAltars;
            bool clickExarch = _dependencies.Settings.ClickExarchAltars;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                if (!TryGetClickableAltarElement(altar, clickEater, clickExarch, out Element? boxToClick))
                    continue;

                foreach (object? step in ClickAltarElement(boxToClick!))
                    yield return step;
            }
        }

        public bool HasClickableAltars()
        {
            bool clickEater = _dependencies.Settings.ClickEaterAltars;
            bool clickExarch = _dependencies.Settings.ClickExarchAltars;
            if (!AltarClickPolicy.ShouldEvaluateAltarScan(clickEater, clickExarch))
                return false;

            IReadOnlyList<PrimaryAltarComponent> altarSnapshot = _dependencies.GetAltarSnapshot();
            if (altarSnapshot.Count == 0)
                return false;

            for (int i = 0; i < altarSnapshot.Count; i++)
                if (TryGetClickableAltarElement(altarSnapshot[i], clickEater, clickExarch, out _))
                    return true;


            return false;
        }

        public bool TryClickManualCursorPreferredAltarOption(Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            IReadOnlyList<PrimaryAltarComponent> altarSnapshot = _dependencies.GetAltarSnapshot();
            if (altarSnapshot.Count == 0)
                return false;

            bool clickEater = _dependencies.Settings.ClickEaterAltars;
            bool clickExarch = _dependencies.Settings.ClickExarchAltars;

            for (int i = 0; i < altarSnapshot.Count; i++)
            {
                if (!TryGetClickableAltarElement(altarSnapshot[i], clickEater, clickExarch, out Element? boxToClick) || boxToClick == null)
                    continue;

                RectangleF optionRect = boxToClick.GetClientRect();
                if (!ManualCursorSelectionMath.IsPointInsideRectInEitherSpace(optionRect, cursorAbsolute, windowTopLeft))
                    continue;

                if (!_dependencies.EnsureCursorInsideGameWindowForClick("[TryClickManualCursorPreferredAltarOption] Skipping click - cursor is outside the PoE window"))
                    return false;

                return TryPerformClick(boxToClick, windowTopLeft, allowWhenHotkeyInactive: true, avoidCursorMove: true);
            }

            return false;
        }

        public bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
            => TryGetClickableAltarElement(altar, clickEater, clickExarch, out _);

        private bool TryGetClickableAltarElement(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, out Element? boxToClick)
        {
            boxToClick = null;

            bool isEnabledType = (altar.AltarType == AltarType.EaterOfWorlds && clickEater)
                || (altar.AltarType == AltarType.SearingExarch && clickExarch);
            if (!isEnabledType)
                return false;

            if (!altar.IsValidCached())
            {
                _dependencies.DebugLog("Skipping altar - Validation failed");
                return false;
            }

            if ((altar.TopMods?.Element?.IsValid != true) || (altar.BottomMods?.Element?.IsValid != true))
            {
                _dependencies.DebugLog("Skipping altar - Elements are not valid");
                return false;
            }

            if ((altar.TopMods?.HasUnmatchedMods == true) || (altar.BottomMods?.HasUnmatchedMods == true))
            {
                _dependencies.DebugLog("Skipping altar - Unmatched mods present");
                return false;
            }

            if (!AreBothAltarOptionsVisibleAndClickable(altar))
            {
                _dependencies.DebugLog("Skipping altar - Top and bottom options are not both visible/clickable yet");
                return false;
            }

            AltarWeights? altarWeights = altar.GetCachedWeights(component => _dependencies.CalculateAltarWeights(component));
            if (!altarWeights.HasValue)
            {
                _dependencies.DebugLog("Skipping altar - Weight calculation failed");
                return false;
            }

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            boxToClick = _dependencies.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);
            if (boxToClick == null)
            {
                _dependencies.DebugLog("Skipping altar - No valid choice could be determined");
                return false;
            }

            if (!_dependencies.IsClickableInEitherSpace(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) || !boxToClick.IsVisible)
            {
                _dependencies.DebugLog("Skipping altar - Choice is not clickable or visible");
                return false;
            }

            return true;
        }

        private IEnumerable ClickAltarElement(Element element)
        {
            _dependencies.DebugLog("[ClickAltarElement] Starting");

            if (element == null)
            {
                _dependencies.LogError("CRITICAL: Altar element is null", 10);
                yield break;
            }

            if (!IsValidVisible(element))
            {
                _dependencies.LogError("CRITICAL: Altar element invalid or not visible before click", 10);
                yield break;
            }

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            if (!_dependencies.EnsureCursorInsideGameWindowForClick("[ClickAltarElement] Skipping click - cursor is outside the PoE window"))
                yield break;

            if (!TryPerformClick(element, windowTopLeft))
                yield break;

            yield return new WaitTime(70);

            bool stillVisible = IsValidVisibleUnderLock(element);
            if (!stillVisible)
            {
                _dependencies.RemoveTrackedAltarByElement(element);
                _dependencies.DebugLog("[ClickAltarElement] Removed clicked altar from tracking (no longer visible)");
            }
            else
                _dependencies.DebugLog("[ClickAltarElement] Altar still visible after click; not removing (possible missclick)");

        }

        private static bool IsValidVisible(Element el)
            => el != null && el.IsValid && el.IsVisible;

        private bool AreBothAltarOptionsVisibleAndClickable(PrimaryAltarComponent altar)
        {
            string altarPath = altar.AltarType.ToString();
            bool topVisibleAndClickable = IsAltarOptionVisibleAndClickable(altar.TopMods?.Element, altarPath);
            bool bottomVisibleAndClickable = IsAltarOptionVisibleAndClickable(altar.BottomMods?.Element, altarPath);
            return AltarClickPolicy.AreBothAltarOptionsActionable(topVisibleAndClickable, bottomVisibleAndClickable);
        }

        private bool IsAltarOptionVisibleAndClickable(Element? optionElement, string altarPath)
        {
            if (optionElement == null || !optionElement.IsValid || !optionElement.IsVisible)
                return false;

            RectangleF optionRect;
            try
            {
                optionRect = optionElement.GetClientRect();
            }
            catch
            {
                return false;
            }

            if (optionRect.Width <= 0 || optionRect.Height <= 0)
                return false;

            return _dependencies.IsClickableInEitherSpace(optionRect.Center, altarPath);
        }

        private bool IsValidVisibleUnderLock(Element el)
        {
            using (LockManager.AcquireStatic(_dependencies.ElementAccessLock))
            {
                return el != null && el.IsValid && el.IsVisible;
            }
        }

        private bool TryPerformClick(Element element, Vector2 windowTopLeft, bool allowWhenHotkeyInactive = false, bool avoidCursorMove = false)
        {
            using (LockManager.AcquireStatic(_dependencies.ElementAccessLock))
            {
                if (!IsValidVisible(element))
                {
                    _dependencies.LogError("[ClickAltarElement] Element became invalid or invisible before click", 10);
                    return false;
                }

                RectangleF rect = element.GetClientRect();
                Vector2 clickPos = rect.Center + windowTopLeft;
                bool executed = _dependencies.ExecuteInteraction(new InteractionExecutionRequest(
                    ClickPosition: clickPos,
                    ExpectedElement: element,
                    Controller: _dependencies.GameController,
                    UseHoldClick: false,
                    HoldDurationMs: 0,
                    ForceUiHoverVerification: false,
                    AllowWhenHotkeyInactive: allowWhenHotkeyInactive,
                    AvoidCursorMove: avoidCursorMove,
                    OutsideWindowLogMessage: "[ClickAltarElement] Skipping altar click - cursor outside PoE window"));

                if (!executed)
                    return false;

                _dependencies.DebugLog("[ClickAltarElement] Click performed");
                return true;
            }
        }
    }
}