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
        object ElementAccessLock,
        Action<string> AddDebugStage);

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

                foreach (object? step in ClickAltarElement(boxToClick!, altar.AltarType.ToString()))
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

                return TryPerformClick(boxToClick, windowTopLeft, altarSnapshot[i].AltarType.ToString(), allowWhenHotkeyInactive: true, avoidCursorMove: true, cursorAbsolute: cursorAbsolute);
            }

            return false;
        }

        public bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
            => TryGetClickableAltarElement(altar, clickEater, clickExarch, out _);

        internal bool ShouldApproachAltar(Entity target)
        {
            long address = target.Address;
            if (address == 0)
                return false;
            IReadOnlyList<PrimaryAltarComponent> altars = _dependencies.GetAltarSnapshot();
            for (int i = 0; i < altars.Count; i++)
            {
                PrimaryAltarComponent altar = altars[i];
                if (altar.SourceEntity?.Address != address)
                    continue;
                bool enabled = altar.AltarType == AltarType.EaterOfWorlds ? _dependencies.Settings.ClickEaterAltars.Value
                    : altar.AltarType == AltarType.SearingExarch && _dependencies.Settings.ClickExarchAltars.Value;
                if (!enabled || !altar.IsValidCached() || !altar.AreModElementsValid()
                    || altar.TopMods.HasUnmatchedMods || altar.BottomMods.HasUnmatchedMods)
                    return false;
                if (!AreBothAltarOptionsVisibleAndClickable(altar, out _, out _))
                    return true;
                AltarWeights? weights = altar.GetCachedWeights(component => _dependencies.CalculateAltarWeights(component));
                if (!weights.HasValue)
                    return false;
                RectangleF top = altar.GetTopModsRect();
                RectangleF bottom = altar.GetBottomModsRect();
                Element? choice = _dependencies.DetermineAltarChoice(altar, weights.Value, top, bottom, top.TopLeft);
                return choice != null && !IsAltarOptionVisibleAndClickable(choice, altar.AltarType.ToString());
            }
            return false;
        }

        private bool TryGetClickableAltarElement(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, out Element? boxToClick)
        {
            boxToClick = null;

            bool isEnabledType = (altar.AltarType == AltarType.EaterOfWorlds && clickEater)
                || (altar.AltarType == AltarType.SearingExarch && clickExarch);
            if (!isEnabledType)
                return false;

            if (!altar.IsValidCached())
            {
                string validation = $"topValid={altar.TopMods?.Element?.IsValid == true} bottomValid={altar.BottomMods?.Element?.IsValid == true} "
                    + $"topUnmatched={altar.TopMods?.HasUnmatchedMods == true} bottomUnmatched={altar.BottomMods?.HasUnmatchedMods == true}";
                RecordSkip("Validation failed", validation);
                return false;
            }

            if ((altar.TopMods?.Element?.IsValid != true) || (altar.BottomMods?.Element?.IsValid != true))
            {
                RecordSkip("Elements are not valid");
                return false;
            }

            if ((altar.TopMods?.HasUnmatchedMods == true) || (altar.BottomMods?.HasUnmatchedMods == true))
            {
                RecordSkip("Unmatched mods present");
                return false;
            }

            if (!AreBothAltarOptionsVisibleAndClickable(altar, out bool topActionable, out bool bottomActionable))
            {
                RecordSkip("Top and bottom options are not both visible/clickable yet", $"top={topActionable} bottom={bottomActionable}");
                return false;
            }

            AltarWeights? altarWeights = altar.GetCachedWeights(component => _dependencies.CalculateAltarWeights(component));
            if (!altarWeights.HasValue)
            {
                RecordSkip("Weight calculation failed");
                return false;
            }

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            boxToClick = _dependencies.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);
            if (boxToClick == null)
            {
                string details = $"top={altarWeights.Value.TopWeight} bottom={altarWeights.Value.BottomWeight}";
                _dependencies.DebugLog($"Skipping altar - No valid choice could be determined ({details})");
                _dependencies.AddDebugStage($"AltarClick: {altar.AltarType} no valid choice ({details})");
                return false;
            }

            if (!IsAltarOptionVisibleAndClickable(boxToClick, altar.AltarType.ToString()))
            {
                RecordSkip("Choice is not clickable or visible", $"visible={boxToClick.IsVisible}");
                return false;
            }

            return true;
        }

        private IEnumerable ClickAltarElement(Element element, string altarPath)
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

            if (!TryPerformClick(element, windowTopLeft, altarPath))
                yield break;

            yield return new WaitTime(70);

            bool stillVisible = IsValidVisibleUnderLock(element);
            if (!stillVisible)
            {
                _dependencies.RemoveTrackedAltarByElement(element);
                _dependencies.DebugLog("[ClickAltarElement] Removed clicked altar from tracking (no longer visible)");
                _dependencies.AddDebugStage("AltarClick: click executed, altar removed from tracking");
            }
            else
            {
                _dependencies.DebugLog("[ClickAltarElement] Altar still visible after click; not removing (possible missclick)");
                _dependencies.AddDebugStage("AltarClick: click executed but altar still visible (possible missclick)");
            }

        }

        private static bool IsValidVisible(Element el)
            => el != null && el.IsValid && el.IsVisible;

        private bool AreBothAltarOptionsVisibleAndClickable(PrimaryAltarComponent altar, out bool topActionable, out bool bottomActionable)
        {
            string altarPath = altar.AltarType.ToString();
            topActionable = IsAltarOptionVisibleAndClickable(altar.TopMods?.Element, altarPath);
            bottomActionable = IsAltarOptionVisibleAndClickable(altar.BottomMods?.Element, altarPath);
            return AltarClickPolicy.AreBothAltarOptionsActionable(topActionable, bottomActionable);
        }

        private void RecordSkip(string reason, string details = "")
        {
            string message = string.IsNullOrEmpty(details)
                ? $"Skipping altar - {reason}"
                : $"Skipping altar - {reason}";
            _dependencies.DebugLog(message);
            _dependencies.AddDebugStage(string.IsNullOrEmpty(details)
                ? $"AltarClick: skipped - {reason}"
                : $"AltarClick: skipped - {reason} ({details})");
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

            return TryResolveAltarClickPoint(optionRect, _dependencies.GameController.Window.GetWindowRectangleTimeCache,
                point => _dependencies.IsClickableInEitherSpace(point, altarPath), out _);
        }

        internal static bool TryResolveAltarClickPoint(RectangleF rect, RectangleF window, Func<Vector2, bool> isClickable, out Vector2 point)
        {
            point = default;
            if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height)
                || rect.Width <= 0 || rect.Height <= 0 || window.Width <= 0 || window.Height <= 0)
                return false;
            return LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, rect.Center, [],
                candidate => candidate.X >= 0 && candidate.Y >= 0 && candidate.X < window.Width && candidate.Y < window.Height
                    && isClickable(candidate), out point);
        }

        private bool IsValidVisibleUnderLock(Element el)
        {
            using (LockManager.AcquireStatic(_dependencies.ElementAccessLock))
            {
                return IsValidVisible(el);
            }
        }

        private bool TryPerformClick(Element element, Vector2 windowTopLeft, string altarPath, bool allowWhenHotkeyInactive = false, bool avoidCursorMove = false, Vector2 cursorAbsolute = default)
        {
            using (LockManager.AcquireStatic(_dependencies.ElementAccessLock))
            {
                if (!IsValidVisible(element))
                {
                    _dependencies.LogError("[ClickAltarElement] Element became invalid or invisible before click", 10);
                    return false;
                }

                RectangleF rect = element.GetClientRect();
                RectangleF window = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
                Vector2 clientPoint;
                if (avoidCursorMove)
                {
                    clientPoint = cursorAbsolute - windowTopLeft;
                    if (!LabelClickPointSearch.IsPointInsideRect(clientPoint, rect)
                        || clientPoint.X < 0 || clientPoint.Y < 0 || clientPoint.X >= window.Width || clientPoint.Y >= window.Height
                        || !_dependencies.IsClickableInEitherSpace(clientPoint, altarPath))
                        return false;
                }
                else if (!TryResolveAltarClickPoint(rect, window, point => _dependencies.IsClickableInEitherSpace(point, altarPath), out clientPoint))
                    return false;
                Vector2 clickPos = clientPoint + windowTopLeft;
                bool executed = _dependencies.ExecuteInteraction(new InteractionExecutionRequest(
                    ClickPosition: clickPos,
                    ExpectedElement: element,
                    Controller: _dependencies.GameController,
                    UseHoldClick: false,
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
