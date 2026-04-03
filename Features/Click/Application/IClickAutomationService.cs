namespace ClickIt.Features.Click.Application
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