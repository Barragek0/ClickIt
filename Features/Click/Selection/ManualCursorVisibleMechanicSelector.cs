namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct ManualCursorVisibleMechanicSelectorDependencies(
        GameController GameController,
        IVisibleMechanicManualInteractionPort VisibleMechanics,
        ClickLabelInteractionService LabelInteraction);

    internal sealed class ManualCursorVisibleMechanicSelector(ManualCursorVisibleMechanicSelectorDependencies dependencies)
    {
        private readonly ManualCursorVisibleMechanicSelectorDependencies _dependencies = dependencies;

        internal bool TryClick(Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            int selectedType = 0;
            float bestDistanceSq = float.MaxValue;
            Vector2 selectedClickPos = default;
            Entity? selectedEntity = null;
            string? selectedSettlersMechanicId = null;

            Entity? shrine = _dependencies.VisibleMechanics.ResolveNextShrineCandidate();
            if (shrine != null)
            {
                var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
                Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                if (ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, shrineClickPos, windowTopLeft, ManualCursorSelectionMath.TargetSnapDistancePx))
                {
                    float d2 = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, shrineClickPos, windowTopLeft);
                    if (d2 < bestDistanceSq)
                    {
                        selectedType = 1;
                        bestDistanceSq = d2;
                        selectedClickPos = shrineClickPos;
                        selectedEntity = shrine;
                        selectedSettlersMechanicId = null;
                    }
                }
            }

            (LostShipmentCandidate? lostShipment, SettlersOreCandidate? settlers) = _dependencies.VisibleMechanics.GetVisibleMechanicCandidates();
            if (lostShipment.HasValue)
            {
                LostShipmentCandidate candidate = lostShipment.Value;
                if (ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft, ManualCursorSelectionMath.TargetSnapDistancePx))
                {
                    float d2 = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft);
                    if (d2 < bestDistanceSq)
                    {
                        selectedType = 2;
                        bestDistanceSq = d2;
                        selectedClickPos = candidate.ClickPosition;
                        selectedEntity = candidate.Entity;
                        selectedSettlersMechanicId = null;
                    }
                }
            }

            if (settlers.HasValue)
            {
                SettlersOreCandidate candidate = settlers.Value;
                if (ManualCursorSelectionMath.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft, ManualCursorSelectionMath.TargetSnapDistancePx))
                {
                    float d2 = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft);
                    if (d2 < bestDistanceSq)
                    {
                        selectedType = 3;
                        bestDistanceSq = d2;
                        selectedClickPos = candidate.ClickPosition;
                        selectedEntity = candidate.Entity;
                        selectedSettlersMechanicId = candidate.MechanicId;
                    }
                }
            }

            if (selectedType == 0)
                return false;

            bool clicked = _dependencies.LabelInteraction.PerformManualCursorInteraction(
                selectedClickPos,
                selectedType == 3 && SettlersMechanicPolicy.RequiresHoldClick(selectedSettlersMechanicId));

            if (!clicked)
                return false;

            if (selectedType == 1)
            {
                _dependencies.VisibleMechanics.HandleSuccessfulShrineClick(selectedEntity);
                return true;
            }

            _dependencies.VisibleMechanics.HandleSuccessfulMechanicEntityClick(selectedEntity);
            return true;
        }
    }
}