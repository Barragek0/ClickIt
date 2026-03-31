namespace ClickIt.Services
{
    internal sealed class CandidateRankingEngine(ClickRuntimeEngine owner)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = owner.Dependencies;

        public RankingResult Rank(ClickTickContext context, ClickCandidates candidates)
        {
            if (!context.GroundItemsVisible)
            {
                return new RankingResult(
                    PreferSettlers: ShouldTryHiddenSettlers(context, candidates),
                    PreferLostShipment: ShouldTryHiddenLostShipment(context, candidates),
                    PreferShrine: ShouldTryHiddenShrine(context),
                    GroundItemsVisible: false);
            }

            return new RankingResult(
                PreferSettlers: ShouldTryVisibleSettlers(context, candidates),
                PreferLostShipment: ShouldTryVisibleLostShipment(context, candidates),
                PreferShrine: _dependencies.LabelSelection.ShouldPreferShrineOverLabel(candidates.NextLabel, context.NextShrine),
                GroundItemsVisible: true);
        }

        private bool ShouldTryHiddenSettlers(ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.SettlersOre.HasValue)
                return false;

            return ClickService.ShouldPreferSettlersWithSharedRankingEngine(
                ClickService.CreateMechanicCandidateSignal(
                    candidates.SettlersOre.Value.MechanicId,
                    candidates.SettlersOre.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                MechanicCandidateSignal.None,
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                    candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                context.MechanicPriorityContext);
        }

        private bool ShouldTryHiddenLostShipment(ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.LostShipment.HasValue)
                return false;

            return ClickService.ShouldPreferLostShipmentWithSharedRankingEngine(
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                MechanicCandidateSignal.None,
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                context.MechanicPriorityContext);
        }

        private static bool ShouldTryHiddenShrine(ClickTickContext context)
        {
            return context.NextShrine != null && ClickService.ShouldClickShrineWhenGroundItemsHidden(context.NextShrine);
        }

        private bool ShouldTryVisibleSettlers(ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.SettlersOre.HasValue)
                return false;

            return ClickService.ShouldPreferSettlersWithSharedRankingEngine(
                ClickService.CreateMechanicCandidateSignal(
                    candidates.SettlersOre.Value.MechanicId,
                    candidates.SettlersOre.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                    candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                context.MechanicPriorityContext);
        }

        private bool ShouldTryVisibleLostShipment(ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.LostShipment.HasValue)
                return false;

            return ClickService.ShouldPreferLostShipmentWithSharedRankingEngine(
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                context.MechanicPriorityContext);
        }
    }
}