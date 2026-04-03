using ClickIt.Features.Labels.Application;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using System.Diagnostics.CodeAnalysis;

namespace ClickIt.Features.Labels.Selection
{
    internal sealed class LabelCandidateBuilderService(LabelMechanicResolutionService mechanicResolutionService)
    {
        private readonly LabelMechanicResolutionService _mechanicResolutionService = mechanicResolutionService;

        public bool TryBuildCandidate(
            LabelOnGround label,
            ClickSettings clickSettings,
            [NotNullWhen(true)] out Entity? item,
            [NotNullWhen(true)] out string? mechanicId,
            out LabelCandidateRejectReason rejectReason)
        {
            return LabelEligibilityEngine.TryBuildCandidate(
                label,
                clickSettings,
                LabelTargetabilityPolicy.IsEntityTargetableForClick,
                _mechanicResolutionService.ResolveMechanicId,
                out item,
                out mechanicId,
                out rejectReason);
        }
    }
}