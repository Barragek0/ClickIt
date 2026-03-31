namespace ClickIt.Services
{
    internal sealed class CandidateRankingEngine(ClickRuntimeEngine owner)
    {
        private readonly ClickRuntimeEngineDependencies _dependencies = owner.Dependencies;

        internal static bool ShouldPreferLostShipmentOverCandidates(
            in MechanicCandidateSignal lostShipment,
            in MechanicCandidateSignal label,
            in MechanicCandidateSignal shrine,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(lostShipment, context, label, shrine);

        internal static bool ShouldPreferSettlersOreOverCandidates(
            in MechanicCandidateSignal settlers,
            in MechanicCandidateSignal label,
            in MechanicCandidateSignal shrine,
            in MechanicCandidateSignal lostShipment,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(settlers, context, label, shrine, lostShipment);

        internal static bool ShouldPreferShrineOverLabel(
            in MechanicCandidateSignal shrine,
            in MechanicCandidateSignal label,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(shrine, context, label);

        internal static MechanicRank BuildRank(float distance, string? mechanicId, in MechanicPriorityContext context)
            => BuildRank(new MechanicCandidateSignal(mechanicId, distance, null), context);

        internal static int CompareRanks(MechanicRank left, MechanicRank right)
            => CandidateScoreEngine.Compare(ToCandidateScore(left), ToCandidateScore(right));

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

            return ShouldPreferSettlersOreOverCandidates(
                new MechanicCandidateSignal(
                    candidates.SettlersOre.Value.MechanicId,
                    candidates.SettlersOre.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                    candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                context.MechanicPriorityContext);
        }

        private bool ShouldTryHiddenLostShipment(ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.LostShipment.HasValue)
                return false;

            return ShouldPreferLostShipmentOverCandidates(
                new MechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                MechanicCandidateSignal.None,
                new MechanicCandidateSignal(
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

            return ShouldPreferSettlersOreOverCandidates(
                new MechanicCandidateSignal(
                    candidates.SettlersOre.Value.MechanicId,
                    candidates.SettlersOre.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.SettlersOre.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.HasValue ? candidates.LostShipment.Value.Distance : null,
                    candidates.LostShipment.HasValue ? ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft) : null),
                context.MechanicPriorityContext);
        }

        private bool ShouldTryVisibleLostShipment(ClickTickContext context, ClickCandidates candidates)
        {
            if (!candidates.LostShipment.HasValue)
                return false;

            return ShouldPreferLostShipmentOverCandidates(
                new MechanicCandidateSignal(
                    ClickService.LostShipmentMechanicId,
                    candidates.LostShipment.Value.Distance,
                    ClickService.GetCursorDistanceSquaredToPoint(candidates.LostShipment.Value.ClickPosition, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    candidates.NextLabelMechanicId,
                    candidates.NextLabel?.ItemOnGround?.DistancePlayer,
                    ClickService.TryGetCursorDistanceSquaredToLabel(candidates.NextLabel, context.CursorAbsolute, context.WindowTopLeft)),
                new MechanicCandidateSignal(
                    ClickService.ShrineMechanicId,
                    context.NextShrine?.DistancePlayer,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(context.NextShrine, context.CursorAbsolute, context.WindowTopLeft)),
                context.MechanicPriorityContext);
        }

        private static bool ShouldPreferCandidate(
            in MechanicCandidateSignal candidate,
            in MechanicPriorityContext context,
            params MechanicCandidateSignal[] others)
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

            var scoreContext = new CandidateScoreEngine.CandidateScoreContext(
                context.PriorityIndexMap,
                context.IgnoreDistanceSet,
                context.IgnoreDistanceWithinByMechanicId,
                context.PriorityDistancePenalty);

            CandidateScoreEngine.CandidateScore score = CandidateScoreEngine.Build(distance, candidate.MechanicId, cursorDistance, scoreContext);
            return new MechanicRank(score.Ignored, score.PriorityIndex, score.WeightedDistance, score.RawDistance, score.CursorDistance);
        }

        private static CandidateScoreEngine.CandidateScore ToCandidateScore(MechanicRank rank)
            => new(rank.Ignored, rank.PriorityIndex, rank.WeightedDistance, rank.RawDistance, rank.CursorDistance);
    }
}