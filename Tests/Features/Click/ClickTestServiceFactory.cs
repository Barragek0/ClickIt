namespace ClickIt.Tests.Features.Click
{
    internal static class ClickTestServiceFactory
    {
        internal static AltarAutomationService CreateAltarAutomationService(
            ClickItSettings settings,
            IReadOnlyList<PrimaryAltarComponent>? snapshot = null,
            Func<string, bool>? ensureCursorInsideGameWindowForClick = null)
        {
            return new AltarAutomationService(new AltarAutomationServiceDependencies(
                Settings: settings,
                GameController: (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController)),
                GetAltarSnapshot: () => snapshot ?? [],
                RemoveTrackedAltarByElement: static _ => { },
                CalculateAltarWeights: static _ => default,
                DetermineAltarChoice: static (_, _, _, _, _) => null,
                IsClickableInEitherSpace: static (_, _) => false,
                EnsureCursorInsideGameWindowForClick: ensureCursorInsideGameWindowForClick ?? (static _ => true),
                ExecuteInteraction: static _ => false,
                DebugLog: static _ => { },
                LogError: static (_, _) => { },
                ElementAccessLock: new object()));
        }

        internal static ClickLabelInteractionService CreateLabelInteractionService(
            GameController? gameController = null,
            ILabelInteractionPort? labelInteractionPort = null,
            Func<LabelOnGround, Vector2, IReadOnlyList<LabelOnGround>?, Func<Vector2, bool>?, (bool Success, Vector2 ClickPos)>? tryResolveClickPosition = null,
            Func<InteractionExecutionRequest, bool>? executeInteraction = null,
            Func<Vector2, string, bool>? isClickableInEitherSpace = null,
            Func<Vector2, bool>? isInsideWindowInEitherSpace = null,
            Func<bool>? groundItemsVisible = null)
        {
            return new ClickLabelInteractionService(new ClickLabelInteractionServiceDependencies(
                Settings: new ClickItSettings(),
                GameController: gameController ?? (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController)),
                LabelInteractionPort: labelInteractionPort ?? (ILabelInteractionPort)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterPort)),
                TryResolveClickPosition: tryResolveClickPosition ?? (static (_, _, _, _) => (false, default)),
                IsClickableInEitherSpace: isClickableInEitherSpace ?? (static (_, _) => false),
                IsInsideWindowInEitherSpace: isInsideWindowInEitherSpace ?? (static _ => false),
                ExecuteInteraction: executeInteraction ?? (static _ => false),
                GroundItemsVisible: groundItemsVisible ?? (static () => false),
                DebugLog: static _ => { }));
        }

        internal static UltimatumAutomationService CreateUltimatumAutomationService(ClickItSettings settings)
        {
            return new UltimatumAutomationService(new UltimatumAutomationServiceDependencies(
                Settings: settings,
                GameController: (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController)),
                CachedLabels: new TimeCache<List<LabelOnGround>>(() => [], 50),
                EnsureCursorInsideGameWindowForClick: static _ => true,
                IsClickableInEitherSpace: static (_, _) => true,
                DebugLog: static _ => { },
                PerformClick: static (_, _) => { },
                RecordClickInterval: static () => { },
                ShouldCaptureUltimatumDebug: static () => false,
                PublishUltimatumDebug: static _ => { }));
        }

        internal static ILabelInteractionPort CreateNoOpLabelInteractionPort()
            => new NoOpLabelInteractionPort();

        private sealed class NoOpLabelInteractionPort : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => null;

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }
    }
}