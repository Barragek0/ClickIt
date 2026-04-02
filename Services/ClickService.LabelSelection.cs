using System.Collections;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ClickIt.Definitions;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ClickIt.Services.Click.Label;
using ClickIt.Services.Click.Runtime;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        [ThreadStatic]
        private static HashSet<long>? _threadGroundLabelEntityAddresses;

        internal bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            // Stable ClickService facade entry point; implementation lives in the label-selection coordinator.
            return LabelSelection.TryClickManualUiHoverLabel(allLabels);
        }

        internal IEnumerator ProcessRegularClick()
        {
            // Stable ClickService facade entry point; orchestration lives in the regular-click coordinator.
            return RegularClick.Run();
        }

        private float? TryGetCursorDistanceSquaredToEntity(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => LabelInteraction.TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft);

        private bool TryCorruptEssence(LabelOnGround label, Vector2 windowTopLeft)
            => LabelInteraction.TryCorruptEssence(label, windowTopLeft);

        private string BuildNoLabelDebugSummary(IReadOnlyList<LabelOnGround>? allLabels)
            => LabelInteraction.BuildNoLabelDebugSummary(allLabels);

        private string BuildLabelRangeRejectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int start, int endExclusive, int examined)
            => LabelInteraction.BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);

        private string BuildLabelSourceDebugSummary(IReadOnlyList<LabelOnGround>? cachedLabelSnapshot)
            => LabelInteraction.BuildLabelSourceDebugSummary(cachedLabelSnapshot);

        private bool IsInsideWindowInEitherSpace(Vector2 point)
        {
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            return ClickLabelSelectionMath.IsInsideWindowInEitherSpace(point, windowArea);
        }

        private bool ShouldSuppressPathfindingLabel(LabelOnGround label)
        {
            return ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                LabelSelection.ShouldSuppressLeverClick(label),
                UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label));
        }

        private IReadOnlyList<LabelOnGround>? GetLabelsForOffscreenSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();
    }
}
