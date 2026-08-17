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
                // Altars are ONLY clicked by the altar automation (which evaluates the top/bottom options and picks the best one). The generic label path must never click an altar label - a blind click lands on a fixed label point (an arbitrary altar option), not the evaluated best choice. Consume the tick so the generic click is skipped; the automation clicks the correct option once the choices become clickable, and the upstream walk decision handles approaching a far altar.
                bool choicesClickable = _dependencies.AltarAutomation.HasClickableAltars();
                _dependencies.DebugLog(choicesClickable
                    ? "[ProcessRegularClick] Item is an altar, breaking"
                    : "[ProcessRegularClick] Item is an altar and altar choices are not fully clickable yet; not clicking");
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