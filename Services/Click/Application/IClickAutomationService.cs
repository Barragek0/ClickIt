using System.Collections;
using ExileCore.PoEMemory.Elements;
using ClickIt.Services.Click.Runtime;

namespace ClickIt.Services.Click.Application
{
    public interface IClickAutomationService
    {
        IEnumerator ProcessRegularClick();
        bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? labels);
        void CancelOffscreenPathingState();
        void CancelPostChestLootSettlementState();
        bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews);
    }
}