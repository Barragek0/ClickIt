using System.Collections;
using ExileCore.PoEMemory.Elements;
using ClickIt.Services;

namespace ClickIt.Core.Runtime
{
    internal sealed class ClickRuntimeHost
    {
        private readonly Func<ClickService?> _resolveClickService;

        public ClickRuntimeHost(Func<ClickService?> resolveClickService)
        {
            _resolveClickService = resolveClickService ?? throw new ArgumentNullException(nameof(resolveClickService));
        }

        public IEnumerator ProcessRegularClick()
        {
            ClickService? clickService = _resolveClickService();
            return clickService?.ProcessRegularClick() ?? Empty();
        }

        public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? labels)
        {
            ClickService? clickService = _resolveClickService();
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