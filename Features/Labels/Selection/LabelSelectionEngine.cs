namespace ClickIt.Features.Labels.Selection
{
    internal readonly record struct LabelCandidateBuildResult(
        bool Success,
        Entity? Item,
        string? MechanicId,
        string? EntityPath,
        LabelCandidateRejectReason RejectReason);

    // Distance + cursor distance for ranking. Production resolves both from a per-label cache keyed on the label address (the DLR reads behind each are the dominant Click-Acquire allocation); tests supply plain values.
    internal readonly record struct LabelRankInput(float Distance, float CursorDistance);

    // Combined per-label scan entry: the candidate build + rank input come from ONE resolution so the selection pass reads the label's live distance/rect a single time (the DLR reads behind them are the dominant LabelScan cost; resolving them twice per label doubled that).
    internal readonly record struct LabelScanEntry(LabelCandidateBuildResult Candidate, LabelRankInput Rank);

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
                LabelCandidateRejectReason.NullItem => this with { NullOrDistanceRejected = NullOrDistanceRejected + 1 },
                LabelCandidateRejectReason.Untargetable => this with { UntargetableRejected = UntargetableRejected + 1 },
                LabelCandidateRejectReason.NotVisible => this with { UntargetableRejected = UntargetableRejected + 1 },
                LabelCandidateRejectReason.LockedChest => this with { UntargetableRejected = UntargetableRejected + 1 },
                LabelCandidateRejectReason.NoMechanic => this with { NoMechanicRejected = NoMechanicRejected + 1 },
                LabelCandidateRejectReason.None => this,
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
        NullItem = 1,
        Untargetable = 2,
        NoMechanic = 3,
        NotVisible = 4,
        LockedChest = 5
    }

    internal static class LabelSelectionEngine
    {
        public static LabelSelectionResult SelectNextLabelByPriority(
            IReadOnlyList<LabelOnGround> allLabels,
            int startIndex,
            int endExclusive,
            ClickSettings clickSettings,
            Func<LabelOnGround, LabelScanEntry> scanEntryResolver,
            Func<LabelOnGround, LabelCandidateBuildResult, bool>? isAcceptable = null)
        {
            if (allLabels.Count == 0)
                return default;

            int start = SystemMath.Max(0, startIndex);
            int end = SystemMath.Min(allLabels.Count, endExclusive);
            if (start >= end)
                return default;

            MechanicCandidateRanker.RankContext scoreContext = new(
                clickSettings.MechanicPriorityIndexMap,
                clickSettings.IgnoreDistanceMechanicIds,
                clickSettings.IgnoreDistanceWithinByMechanicId,
                clickSettings.MechanicPriorityDistancePenalty);

            LabelSelectionStats stats = default;
            LabelOnGround? bestCandidate = null;
            MechanicRank bestScore = default;
            bool hasBestScore = false;
            string? bestMechanicId = null;

            for (int i = start; i < end; i++)
            {
                LabelOnGround label = allLabels[i];
                stats = stats.IncrementConsidered();

                LabelScanEntry entry = scanEntryResolver(label);
                LabelCandidateBuildResult candidate = entry.Candidate;
                if (!candidate.Success)
                {
                    stats = stats.AddReject(candidate.RejectReason);
                    continue;
                }

                LabelRankInput rankInput = entry.Rank;
                MechanicRank score = MechanicCandidateRanker.Build(
                    rankInput.Distance,
                    candidate.MechanicId,
                    rankInput.CursorDistance,
                    scoreContext);

                if (score.Ignored)
                    stats = stats.IncrementIgnoredByDistance();

                bool isCrawler = candidate.MechanicId == MechanicIds.DelveCrawler;
                bool bestIsCrawler = bestMechanicId == MechanicIds.DelveCrawler;
                if (!hasBestScore || (isCrawler != bestIsCrawler ? !isCrawler : MechanicCandidateRanker.Compare(score, bestScore) < 0))
                {
                    // Only resolve expensive clickability for a candidate that could improve the current choice.
                    if (isAcceptable != null && !isAcceptable(label, candidate))
                        continue;

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
