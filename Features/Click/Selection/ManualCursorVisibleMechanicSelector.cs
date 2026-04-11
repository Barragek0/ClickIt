namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct ManualCursorVisibleMechanicSelection(
        Vector2 ClickPosition,
        Entity? Entity,
        string? SettlersMechanicId,
        bool IsShrine);

    internal readonly record struct ManualCursorVisibleMechanicSelectorDependencies(
        GameController GameController,
        IVisibleMechanicRuntimePort VisibleMechanics,
        ClickLabelInteractionService LabelInteraction);

    internal sealed class ManualCursorVisibleMechanicSelector(ManualCursorVisibleMechanicSelectorDependencies dependencies)
    {
        private readonly ManualCursorVisibleMechanicSelectorDependencies _dependencies = dependencies;

        internal bool TryClick(Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            float bestDistanceSq = float.MaxValue;
            ManualCursorVisibleMechanicSelection? selected = null;

            Entity? shrine = _dependencies.VisibleMechanics.ResolveNextShrineCandidate();
            if (shrine != null && TryProjectEntityScreenPosition(_dependencies.GameController, shrine, out Vector2 shrineClickPos))
            {
                TrySelectCandidate(
                    cursorAbsolute,
                    windowTopLeft,
                    shrineClickPos,
                    shrine,
                    settlersMechanicId: null,
                    isShrine: true,
                    ref bestDistanceSq,
                    ref selected);
            }

            VisibleMechanicSelectionSnapshot visibleMechanicSelection = _dependencies.VisibleMechanics.GetVisibleMechanicSelectionSnapshot();
            if (visibleMechanicSelection.LostShipment.HasValue)
            {
                LostShipmentCandidate candidate = visibleMechanicSelection.LostShipment.Value;
                TrySelectCandidate(
                    cursorAbsolute,
                    windowTopLeft,
                    candidate.ClickPosition,
                    candidate.Entity,
                    settlersMechanicId: null,
                    isShrine: false,
                    ref bestDistanceSq,
                    ref selected);
            }

            if (visibleMechanicSelection.Settlers.HasValue)
            {
                SettlersOreCandidate candidate = visibleMechanicSelection.Settlers.Value;
                TrySelectCandidate(
                    cursorAbsolute,
                    windowTopLeft,
                    candidate.ClickPosition,
                    candidate.Entity,
                    candidate.MechanicId,
                    isShrine: false,
                    ref bestDistanceSq,
                    ref selected);
            }

            if (!selected.HasValue)
                return false;

            ManualCursorVisibleMechanicSelection resolvedSelection = selected.Value;
            bool clicked = _dependencies.LabelInteraction.PerformManualCursorInteraction(
                resolvedSelection.ClickPosition,
                !resolvedSelection.IsShrine && SettlersMechanicPolicy.RequiresHoldClick(resolvedSelection.SettlersMechanicId));

            if (!clicked)
                return false;

            if (resolvedSelection.IsShrine)
            {
                _dependencies.VisibleMechanics.HandleSuccessfulShrineClick(resolvedSelection.Entity);
                return true;
            }

            _dependencies.VisibleMechanics.HandleSuccessfulMechanicEntityClick(resolvedSelection.Entity);
            return true;
        }

        private static void TrySelectCandidate(
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            Vector2 clickPosition,
            Entity? entity,
            string? settlersMechanicId,
            bool isShrine,
            ref float bestDistanceSq,
            ref ManualCursorVisibleMechanicSelection? selected)
        {
            if (!ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(
                cursorAbsolute,
                clickPosition,
                windowTopLeft,
                ManualCursorSelectionMath.TargetSnapDistancePx))
            {
                return;
            }

            float distanceSquared = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, clickPosition, windowTopLeft);
            if (distanceSquared >= bestDistanceSq)
                return;

            bestDistanceSq = distanceSquared;
            selected = new ManualCursorVisibleMechanicSelection(clickPosition, entity, settlersMechanicId, isShrine);
        }

        private static bool TryProjectEntityScreenPosition(GameController gameController, Entity entity, out Vector2 clickPosition)
        {
            clickPosition = default;

            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.PosNum, out object? rawPosition)
                || rawPosition is not System.Numerics.Vector3 position
                || !DynamicAccess.TryGetDynamicValue(gameController, DynamicAccessProfiles.Game, out object? rawGame)
                || !DynamicAccess.TryGetDynamicValue(rawGame, DynamicAccessProfiles.IngameState, out object? rawIngameState)
                || !DynamicAccess.TryGetDynamicValue(rawIngameState, DynamicAccessProfiles.Camera, out object? rawCamera)
                || !DynamicAccess.TryProjectWorldToScreen(rawCamera, position, out object? rawProjected)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.X, out float projectedX)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.Y, out float projectedY))
            {
                return false;
            }

            clickPosition = new(projectedX, projectedY);
            return true;
        }
    }
}