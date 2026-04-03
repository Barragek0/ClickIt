namespace ClickIt.Features.Labels
{
    public class LabelService
    {
        private readonly LabelReadModelService _readModelService;

        public TimeCache<List<LabelOnGround>> CachedLabels { get; }

        public LabelService(LabelReadModelService readModelService)
        {
            _readModelService = readModelService ?? throw new ArgumentNullException(nameof(readModelService));
            CachedLabels = _readModelService.CachedLabels;
        }

        public bool GroundItemsVisible()
        {
            return _readModelService.GroundItemsVisible();
        }

        public List<LabelOnGround> UpdateLabelComponent()
            => _readModelService.UpdateLabelComponent();
    }
}
