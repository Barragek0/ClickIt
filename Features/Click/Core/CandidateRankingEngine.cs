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

        internal static DecisionResult Rank(
            LabelSelectionScanEngine labelSelectionScan,
            ClickLabelInteractionService labelInteraction,
            ClickTickContext context,
            ClickCandidates candidates)
            => new(
                TrySettlers: candidates.SettlersOre is { } settlers && ShouldPreferSettlersOreOverCandidates(
                    new MechanicCandidateSignal(settlers.MechanicId, settlers.Distance,
                        ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(settlers.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                    ReadLabelRankSignal(context, candidates),
                    ReadShrineRankSignal(labelInteraction, context),
                    ReadShipmentRankSignal(context, candidates.LostShipment),
                    context.MechanicPriorityContext),
                TryLostShipment: candidates.LostShipment.HasValue && ShouldPreferLostShipmentOverCandidates(
                    ReadShipmentRankSignal(context, candidates.LostShipment),
                    ReadLabelRankSignal(context, candidates),
                    ReadShrineRankSignal(labelInteraction, context),
                    context.MechanicPriorityContext),
                TryShrine: context.GroundItemsVisible
                    ? labelSelectionScan.ShouldPreferShrineOverLabel(candidates.NextLabel, context.NextShrine)
                    : context.NextShrine != null,
                GroundItemsVisible: context.GroundItemsVisible);

        private static MechanicCandidateSignal ReadLabelRankSignal(in ClickTickContext context, in ClickCandidates candidates)
            => context.GroundItemsVisible
                ? new MechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ManualCursorSelectionMath.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft))
                : MechanicCandidateSignal.None;

        private static MechanicCandidateSignal ReadShrineRankSignal(ClickLabelInteractionService labelInteraction, in ClickTickContext context)
            => new(MechanicIds.Shrines, context.NextShrine?.DistancePlayer,
                labelInteraction.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft));

        private static MechanicCandidateSignal ReadShipmentRankSignal(in ClickTickContext context, LostShipmentCandidate? shipment)
            => new(MechanicIds.LostShipment, shipment?.Distance, shipment is { } candidate
                ? ManualCursorSelectionMath.GetCursorDistanceSquaredToPoint(candidate.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)
                : null);

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

                int tier = MechanicIds.GetFallbackTier(candidate.MechanicId);
                int otherTier = MechanicIds.GetFallbackTier(other.MechanicId);
                if (tier < otherTier)
                    continue;
                if (tier > otherTier)
                    return false;

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
