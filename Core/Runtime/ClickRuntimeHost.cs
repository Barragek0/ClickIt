namespace ClickIt.Core.Runtime
{
    public sealed class ClickRuntimeHost(Func<IClickAutomationService?> resolveClickService)
    {
        private readonly Func<IClickAutomationService?> _resolveClickService = resolveClickService ?? throw new ArgumentNullException(nameof(resolveClickService));

        public IEnumerator ProcessRegularClick()
        {
            IClickAutomationService? clickService = _resolveClickService();
            return clickService?.ProcessRegularClick() ?? Empty();
        }

        public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? labels)
        {
            IClickAutomationService? clickService = _resolveClickService();
            return clickService?.TryClickManualUiHoverLabel(labels) == true;
        }

        public void CancelOffscreenPathingState()
        {
            _resolveClickService()?.CancelOffscreenPathingState();
        }

        public void CancelPostChestLootSettlementState()
        {
            _resolveClickService()?.CancelPostChestLootSettlementState();
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }
}