using ClickIt.Services.Click.Runtime;

namespace ClickIt.Services.Click.Application
{
    internal delegate bool UltimatumOptionPreviewResolver(out List<UltimatumPanelOptionPreview> previews);

    internal sealed class UltimatumAutomationService(
        UltimatumOptionPreviewResolver tryGetPanelPreview,
        UltimatumOptionPreviewResolver tryGetGroundPreview)
    {
        private readonly UltimatumOptionPreviewResolver _tryGetPanelPreview = tryGetPanelPreview;
        private readonly UltimatumOptionPreviewResolver _tryGetGroundPreview = tryGetGroundPreview;

        public bool TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (_tryGetPanelPreview(out previews) && previews.Count > 0)
                return true;

            return _tryGetGroundPreview(out previews);
        }
    }
}