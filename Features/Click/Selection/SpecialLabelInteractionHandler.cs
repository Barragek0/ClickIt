namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct SpecialLabelInteractionHandlerDependencies(
        ClickItSettings Settings,
        AltarAutomationService AltarAutomation,
        ClickLabelInteractionService LabelInteraction,
        UltimatumAutomationService UltimatumAutomation,
        Action<string> DebugLog);

    internal sealed class SpecialLabelInteractionHandler(SpecialLabelInteractionHandlerDependencies dependencies)
    {
        private readonly SpecialLabelInteractionHandlerDependencies _dependencies = dependencies;

        public bool TryHandle(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            if (nextLabel == null)
                return false;

            if (ClickLabelSelectionMath.IsAltarLabel(nextLabel))
            {
                bool shouldContinuePathing = ClickLabelSelectionMath.ShouldContinuePathingForSpecialAltarLabel(
                    _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                    nextLabel.ItemOnGround != null,
                    nextLabel.ItemOnGround?.IsHidden == true,
                    _dependencies.AltarAutomation.HasClickableAltars());
                if (shouldContinuePathing)
                {
                    _dependencies.DebugLog("[ProcessRegularClick] Item is an altar and altar choices are not fully clickable yet; continuing pathing");
                    return false;
                }

                _dependencies.DebugLog("[ProcessRegularClick] Item is an altar, breaking");
                return true;
            }

            if (_dependencies.LabelInteraction.TryCorruptEssence(nextLabel, windowTopLeft))
                return true;

            if (!_dependencies.Settings.IsInitialUltimatumClickEnabled() || !UltimatumLabelMath.IsUltimatumLabel(nextLabel))
                return false;

            if (_dependencies.UltimatumAutomation.TryClickPreferredModifier(nextLabel, windowTopLeft))
                return true;

            _dependencies.DebugLog("[ProcessRegularClick] Ultimatum label detected but no preferred modifier matched; skipping generic label click");
            return true;
        }
    }
}