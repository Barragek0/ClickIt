namespace ClickIt.Features.Labels.Selection
{
    internal readonly record struct LabelCandidateBuildResult(
        bool Success,
        Entity? Item,
        string? MechanicId,
        LabelCandidateRejectReason RejectReason);

    internal readonly record struct LabelSelectionStats(
        int ConsideredCandidates,
        int NullOrDistanceRejected,
        int UntargetableRejected,
        int NoMechanicRejected,
        int IgnoredByDistanceCandidates)
    {
        public LabelSelectionStats IncrementConsidered()
            => this with { ConsideredCandidates = ConsideredCandidates + 1 };

        public LabelSelectionStats IncrementIgnoredByDistance()
            => this with { IgnoredByDistanceCandidates = IgnoredByDistanceCandidates + 1 };

        public LabelSelectionStats AddReject(LabelCandidateRejectReason rejectReason)
            => rejectReason switch
            {
                LabelCandidateRejectReason.NullItemOrOutOfDistance => this with { NullOrDistanceRejected = NullOrDistanceRejected + 1 },
                LabelCandidateRejectReason.Untargetable => this with { UntargetableRejected = UntargetableRejected + 1 },
                LabelCandidateRejectReason.NoMechanic => this with { NoMechanicRejected = NoMechanicRejected + 1 },
                _ => this,
            };
    }

    internal readonly record struct LabelSelectionResult(
        LabelOnGround? SelectedCandidate,
        string? SelectedMechanicId,
        LabelSelectionStats Stats);

    internal enum LabelCandidateRejectReason
    {
        None = 0,
        NullItemOrOutOfDistance = 1,
        Untargetable = 2,
        NoMechanic = 3
    }

    internal static class LabelSelectionEngine
    {
        public static LabelSelectionResult SelectNextLabelByPriority(
            IReadOnlyList<LabelOnGround> allLabels,
            int startIndex,
            int endExclusive,
            ClickSettings clickSettings,
            Func<LabelOnGround, LabelCandidateBuildResult> candidateBuilder,
            Func<LabelOnGround, float> cursorDistanceResolver)
        {
            if (allLabels.Count == 0)
                return default;

            int start = Math.Max(0, startIndex);
            int end = Math.Min(allLabels.Count, endExclusive);
            if (start >= end)
                return default;

            var scoreContext = new MechanicCandidateRanker.RankContext(
                clickSettings.MechanicPriorityIndexMap,
                clickSettings.IgnoreDistanceMechanicIds,
                clickSettings.IgnoreDistanceWithinByMechanicId,
                clickSettings.MechanicPriorityDistancePenalty);

            LabelSelectionStats stats = default;
            LabelOnGround? bestCandidate = null;
            MechanicCandidateRanker.CandidateRank bestScore = default;
            bool hasBestScore = false;
            string? bestMechanicId = null;

            for (int i = start; i < end; i++)
            {
                LabelOnGround label = allLabels[i];
                stats = stats.IncrementConsidered();

                LabelCandidateBuildResult candidate = candidateBuilder(label);
                if (!candidate.Success)
                {
                    stats = stats.AddReject(candidate.RejectReason);
                    continue;
                }

                float cursorDistance = cursorDistanceResolver(label);
                MechanicCandidateRanker.CandidateRank score = MechanicCandidateRanker.Build(
                    candidate.Item!.DistancePlayer,
                    candidate.MechanicId!,
                    cursorDistance,
                    scoreContext);

                if (score.Ignored)
                    stats = stats.IncrementIgnoredByDistance();

                if (!hasBestScore || MechanicCandidateRanker.Compare(score, bestScore) < 0)
                {
                    bestCandidate = label;
                    bestScore = score;
                    hasBestScore = true;
                    bestMechanicId = candidate.MechanicId;
                }
            }

            return new LabelSelectionResult(bestCandidate, bestMechanicId, stats);
        }
    }
}