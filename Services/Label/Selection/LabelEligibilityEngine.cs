using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Selection
{
    internal static class LabelEligibilityEngine
    {
        internal static bool TryBuildCandidate(
            LabelOnGround label,
            ClickSettings clickSettings,
            Func<LabelOnGround, Entity, bool> isTargetableForClick,
            Func<LabelOnGround, Entity, ClickSettings, string?> resolveMechanicId,
            out Entity? item,
            out string? mechanicId,
            out LabelCandidateRejectReason rejectReason)
        {
            item = label.ItemOnGround;
            mechanicId = null;
            rejectReason = LabelCandidateRejectReason.None;

            if (item == null || item.DistancePlayer > clickSettings.ClickDistance)
            {
                rejectReason = LabelCandidateRejectReason.NullItemOrOutOfDistance;
                return false;
            }

            if (!isTargetableForClick(label, item))
            {
                rejectReason = LabelCandidateRejectReason.Untargetable;
                return false;
            }

            mechanicId = resolveMechanicId(label, item, clickSettings);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                rejectReason = LabelCandidateRejectReason.NoMechanic;
                return false;
            }

            return true;
        }
    }
}