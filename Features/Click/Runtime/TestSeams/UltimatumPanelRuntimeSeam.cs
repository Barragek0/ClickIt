namespace ClickIt.Features.Click.Runtime.TestSeams
{
    /** This seam isolates concrete ExileCore Ultimatum panel traversal behind a single boundary because the owner logic depends on third-party panel objects whose visibility, child-path, and element-state getters are not safely shapeable in the current test harness. Keep the default implementation behavior-equivalent to the underlying runtime helpers; tests may replace it only to model that external boundary. */
    internal interface IUltimatumPanelRuntimeSeam
    {
        bool TryGetVisiblePanel(GameController? gameController, bool logFailures, Action<string> debugLog, out UltimatumPanel? panelObj);

        bool TryCollectPanelChoiceCandidates(
            UltimatumPanel panelObj,
            IReadOnlyList<string> priorities,
            bool isGruelingGauntletActive,
            bool logFailures,
            Action<string> debugLog,
            out List<UltimatumPanelChoiceCandidate> candidates);

        bool TryResolveConfirmButton(UltimatumPanel panelObj, Action<string> debugLog, out Element resolved);

        bool TryResolveTakeRewardsButton(UltimatumPanel panelObj, Action<string> debugLog, out Element resolved);
    }

    internal sealed class UltimatumPanelRuntimeSeam : IUltimatumPanelRuntimeSeam
    {
        internal static IUltimatumPanelRuntimeSeam Instance { get; } = new UltimatumPanelRuntimeSeam();

        private UltimatumPanelRuntimeSeam()
        {
        }

        public bool TryGetVisiblePanel(GameController? gameController, bool logFailures, Action<string> debugLog, out UltimatumPanel? panelObj)
            => UltimatumPanelUiQuery.TryGetVisiblePanel(gameController, logFailures, debugLog, out panelObj);

        public bool TryCollectPanelChoiceCandidates(
            UltimatumPanel panelObj,
            IReadOnlyList<string> priorities,
            bool isGruelingGauntletActive,
            bool logFailures,
            Action<string> debugLog,
            out List<UltimatumPanelChoiceCandidate> candidates)
            => UltimatumPanelChoiceCollector.TryCollectCandidates(panelObj, priorities, isGruelingGauntletActive, logFailures, debugLog, out candidates);

        public bool TryResolveConfirmButton(UltimatumPanel panelObj, Action<string> debugLog, out Element resolved)
            => UltimatumPanelButtonResolver.TryResolveConfirmButton(panelObj.ConfirmButton, debugLog, out resolved);

        public bool TryResolveTakeRewardsButton(UltimatumPanel panelObj, Action<string> debugLog, out Element resolved)
        {
            Element? takeRewardsElement = panelObj.GetChildAtIndex(1)?.GetChildAtIndex(4)?.GetChildAtIndex(0);
            return UltimatumPanelButtonResolver.TryResolveTakeRewardsButton(takeRewardsElement, debugLog, out resolved);
        }
    }
}