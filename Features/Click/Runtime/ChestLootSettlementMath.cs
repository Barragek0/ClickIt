namespace ClickIt.Features.Click.Runtime
{
    internal static class ChestLootSettlementMath
    {
        internal const string HeistChestSettleMechanicId = "heist-chests";

        internal const int DefaultInitialDelayMs = 500;
        internal const int DefaultPollIntervalMs = 100;
        internal const int DefaultQuietWindowMs = 500;

        internal static LabelOnGround? FindPendingChestLabel(IReadOnlyList<LabelOnGround>? allLabels, long itemAddress, long labelAddress)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? label = allLabels[i];
                if (label == null)
                    continue;

                long currentItemAddress = label.ItemOnGround?.Address ?? 0;
                long currentLabelAddress = label.Label?.Address ?? 0;
                if ((itemAddress != 0 && currentItemAddress == itemAddress)
                    || (labelAddress != 0 && currentLabelAddress == labelAddress))
                    return label;

            }

            return null;
        }

        internal static void SeedKnownGroundItemAddresses(HashSet<long> knownAddresses, IReadOnlySet<long>? snapshot)
        {
            knownAddresses.Clear();
            _ = MergeNewGroundItemAddresses(knownAddresses, snapshot);
        }

        internal static bool MergeNewGroundItemAddresses(HashSet<long> knownAddresses, IReadOnlySet<long>? snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
                return false;

            bool addedAny = false;
            foreach (long address in snapshot)
            {
                if (address == 0)
                    continue;
                if (knownAddresses.Add(address))
                    addedAny = true;
            }

            return addedAny;
        }

        internal static bool IsMechanicEligibleForNearbyChestLootSettlementBypass(string? mechanicId)
            => !string.IsNullOrWhiteSpace(mechanicId);

        internal static bool IsWithinNearbyChestLootSettlementBypassDistance(Vector2 sourceGridPos, Vector2 entityGridPos, int maxDistance)
        {
            if (maxDistance < 0)
                return false;

            float maxDistanceSq = maxDistance * maxDistance;
            float distanceSq = CoordinateSpace.DistanceSquared(sourceGridPos, entityGridPos);
            return distanceSq <= maxDistanceSq;
        }

        internal static bool TryGetEntityGridPosition(Entity? entity, out Vector2 gridPos)
        {
            gridPos = default;
            if (entity == null || !entity.IsValid)
                return false;

            var grid = entity.GridPosNum;
            gridPos = new Vector2(grid.X, grid.Y);
            return true;
        }

        internal static bool ShouldWaitForChestLootSettlement(
            string? mechanicId,
            bool waitAfterOpeningBasicChests,
            bool waitAfterOpeningLeagueChests,
            bool waitAfterOpeningHeistChests)
        {
            if (string.Equals(mechanicId, MechanicIds.BasicChests, StringComparison.OrdinalIgnoreCase))
                return waitAfterOpeningBasicChests;

            if (string.Equals(mechanicId, HeistChestSettleMechanicId, StringComparison.OrdinalIgnoreCase))
                return waitAfterOpeningHeistChests;

            if (string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase))
                return waitAfterOpeningLeagueChests;

            return false;
        }

        internal static string? ResolveChestLootSettlementMechanicIdForOpenedLabel(
            string? mechanicId,
            string? entityPath,
            string? entityRenderName)
        {
            if (!IsLeagueChestMechanic(mechanicId))
                return mechanicId;

            return IsHeistChestPath(entityPath) || IsHeistChestRenderName(entityRenderName)
                ? HeistChestSettleMechanicId
                : MechanicIds.LeagueChests;
        }

        internal static ChestLootSettlementTiming ResolvePostChestLootSettlementTimingSettings(
            string? mechanicId,
            in ChestLootSettlementTimingOptions options)
        {
            if (string.Equals(mechanicId, MechanicIds.BasicChests, StringComparison.OrdinalIgnoreCase))
                return NormalizeChestLootSettlementTiming(options.Shared);


            if (string.Equals(mechanicId, HeistChestSettleMechanicId, StringComparison.OrdinalIgnoreCase))
                return NormalizeChestLootSettlementTiming(options.Shared);


            if (string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase))
                return NormalizeChestLootSettlementTiming(options.Shared);


            return new ChestLootSettlementTiming(
                DefaultInitialDelayMs,
                DefaultPollIntervalMs,
                DefaultQuietWindowMs);
        }

        internal static bool ShouldContinueChestOpenRetries(bool pendingChestOpenConfirmationActive, bool chestLabelVisible)
            => pendingChestOpenConfirmationActive && chestLabelVisible;

        internal static bool ShouldStartChestLootSettlementAfterClick(bool pendingChestOpenConfirmationActive, bool chestLabelVisible)
            => pendingChestOpenConfirmationActive && !chestLabelVisible;

        internal static bool IsChestLootSettlementQuietPeriodElapsed(
            long now,
            long lastNewGroundItemTimestampMs,
            int quietWindowMs,
            out long remainingMs)
        {
            if (quietWindowMs <= 0)
            {
                remainingMs = 0;
                return true;
            }

            if (lastNewGroundItemTimestampMs <= 0)
            {
                remainingMs = quietWindowMs;
                return false;
            }

            long elapsed = SystemMath.Max(0, now - lastNewGroundItemTimestampMs);
            if (elapsed >= quietWindowMs)
            {
                remainingMs = 0;
                return true;
            }

            remainingMs = quietWindowMs - elapsed;
            return false;
        }

        private static bool IsHeistChestPath(string? entityPath)
            => !string.IsNullOrWhiteSpace(entityPath)
               && entityPath.Contains("/LeagueHeist/", StringComparison.OrdinalIgnoreCase);

        private static bool IsLeagueChestMechanic(string? mechanicId)
            => string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.MirageGoldenDjinnCache, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.MirageSilverDjinnCache, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.MirageBronzeDjinnCache, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.HeistSecureLocker, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.HeistSecureRepository, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.BlightCyst, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.BreachGraspingCoffers, StringComparison.OrdinalIgnoreCase)
               || string.Equals(mechanicId, MechanicIds.SynthesisSynthesisedStash, StringComparison.OrdinalIgnoreCase);

        private static bool IsHeistChestRenderName(string? entityRenderName)
            => !string.IsNullOrWhiteSpace(entityRenderName)
               && (entityRenderName.Contains("Secure Locker", StringComparison.OrdinalIgnoreCase)
                   || entityRenderName.Contains("Secure Repository", StringComparison.OrdinalIgnoreCase));

        private static ChestLootSettlementTiming NormalizeChestLootSettlementTiming(in ChestLootSettlementTiming timing)
            => new(
                SystemMath.Max(0, timing.InitialDelayMs),
                SystemMath.Max(1, timing.PollIntervalMs),
                SystemMath.Max(0, timing.QuietWindowMs));
    }
}