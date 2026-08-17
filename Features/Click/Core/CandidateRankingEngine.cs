namespace ClickIt.Features.Click.Core
{
    internal static class CandidateRankingEngine
    {
        internal static bool ShouldPreferLostShipmentOverCandidates(
            in MechanicCandidateSignal lostShipment,
            in MechanicCandidateSignal label,
            in MechanicCandidateSignal shrine,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(lostShipment, context, [label, shrine]);

        internal static bool ShouldPreferSettlersOreOverCandidates(
            in MechanicCandidateSignal settlers,
            in MechanicCandidateSignal label,
            in MechanicCandidateSignal shrine,
            in MechanicCandidateSignal lostShipment,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(settlers, context, [label, shrine, lostShipment]);

        internal static bool ShouldPreferShrineOverLabel(
            in MechanicCandidateSignal shrine,
            in MechanicCandidateSignal label,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(shrine, context, [label]);

        internal static MechanicRank BuildRank(float distance, string? mechanicId, in MechanicPriorityContext context)
            => BuildRank(new MechanicCandidateSignal(mechanicId, distance, null), context);

        internal static int CompareRanks(MechanicRank left, MechanicRank right)
            => MechanicCandidateRanker.Compare(left, right);

        internal static RankingResult Rank(
            LabelSelectionScanEngine labelSelectionScan,
            ClickLabelInteractionService labelInteraction,
            ClickTickContext context,
            ClickCandidates candidates)
        {
            if (!context.GroundItemsVisible)
            {
                return new RankingResult(
                    PreferSettlers: ShouldTryHiddenSettlers(labelInteraction, context, candidates),
                    PreferLostShipment: ShouldTryHiddenLostShipment(labelInteraction, context, candidates),
                    PreferShrine: ShouldTryHiddenShrine(context),
                    GroundItemsVisible: false);
            }

            return new RankingResult(
                PreferSettlers: ShouldTryVisibleSettlers(labelInteraction, context, candidates),
                PreferLostShipment: ShouldTryVisibleLostShipment(labelInteraction, context, candidates),
                PreferShrine: labelSelectionScan.ShouldPreferShrineOverLabel(candidates.NextLabel, context.NextShrine),
                GroundItemsVisible: true);
        }

        private static bool ShouldTryHiddenSettlers(ClickLabelInteractionService labelInteraction, ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.SettlersOre.HasValue)
                return false;

            return ShouldPreferSettlersOreOverCandidates(
                new MechanicCandidateSignal(
                    candidates.SettlersOre.Value.MechanicId,
                    candidates.SettlersOre.Value.Distance,
                    ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(
                    MechanicIds.Shrines,
                    context.NextShrine?.DistancePlayer,
                    labelInteraction.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    MechanicIds.LostShipment,
                    candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                    candidates.LostShipment.HasValue ? ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                context.MechanicPriorityContext);
        }

        private static bool ShouldTryHiddenLostShipment(ClickLabelInteractionService labelInteraction, ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.LostShipment.HasValue)
                return false;

            return ShouldPreferLostShipmentOverCandidates(
                new MechanicCandidateSignal(
                    MechanicIds.LostShipment,
                    candidates.LostShipment.Value.Distance,
                    ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(
                    MechanicIds.Shrines,
                    context.NextShrine?.DistancePlayer,
                    labelInteraction.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                context.MechanicPriorityContext);
        }

        private static bool ShouldTryHiddenShrine(ClickTickContext context)
            => context.NextShrine != null;

        private static bool ShouldTryVisibleSettlers(ClickLabelInteractionService labelInteraction, ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.SettlersOre.HasValue)
                return false;

            return ShouldPreferSettlersOreOverCandidates(
                new MechanicCandidateSignal(
                    candidates.SettlersOre.Value.MechanicId,
                    candidates.SettlersOre.Value.Distance,
                    ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ManualCursorSelectionMath.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    MechanicIds.Shrines,
                    context.NextShrine?.DistancePlayer,
                    labelInteraction.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    MechanicIds.LostShipment,
                    candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                    candidates.LostShipment.HasValue ? ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                context.MechanicPriorityContext);
        }

        private static bool ShouldTryVisibleLostShipment(ClickLabelInteractionService labelInteraction, ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.LostShipment.HasValue)
                return false;

            return ShouldPreferLostShipmentOverCandidates(
                new MechanicCandidateSignal(
                    MechanicIds.LostShipment,
                    candidates.LostShipment.Value.Distance,
                    ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ManualCursorSelectionMath.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    MechanicIds.Shrines,
                    context.NextShrine?.DistancePlayer,
                    labelInteraction.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                context.MechanicPriorityContext);
        }

        private static bool ShouldPreferCandidate(
            in MechanicCandidateSignal candidate,
            in MechanicPriorityContext context,
            ReadOnlySpan<MechanicCandidateSignal> others)
        {
            if (!candidate.Exists)
                return false;

            MechanicRank candidateRank = BuildRank(candidate, context);
            for (int i = 0; i < others.Length; i++)
            {
                MechanicCandidateSignal other = others[i];
                if (!other.Exists)
                    continue;

                MechanicRank otherRank = BuildRank(other, context);
                if (CompareRanks(candidateRank, otherRank) >= 0)
                    return false;
            }

            return true;
        }

        private static MechanicRank BuildRank(in MechanicCandidateSignal candidate, in MechanicPriorityContext context)
        {
            float distance = candidate.Distance ?? float.MaxValue;
            float cursorDistance = candidate.CursorDistance ?? float.MaxValue;

            MechanicCandidateRanker.RankContext scoreContext = new(
                context.PriorityIndexMap,
                context.IgnoreDistanceSet,
                context.IgnoreDistanceWithinByMechanicId,
                context.PriorityDistancePenalty);

            return MechanicCandidateRanker.Build(distance, candidate.MechanicId, cursorDistance, scoreContext);
        }
    }
}