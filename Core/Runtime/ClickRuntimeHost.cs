using System.Collections;
using ExileCore.PoEMemory.Elements;
using ClickIt.Services;
using ClickIt.Services.Click.Application;

namespace ClickIt.Core.Runtime
{
    public sealed class ClickRuntimeHost
    {
        private readonly Func<IClickAutomationService?> _resolveClickService;

        public ClickRuntimeHost(Func<IClickAutomationService?> resolveClickService)
        {
            _resolveClickService = resolveClickService ?? throw new ArgumentNullException(nameof(resolveClickService));
        }

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