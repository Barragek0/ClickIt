namespace ClickIt.Tests.Features.Labels.Application
{
    [TestClass]
    public class LazyModeBlockerServiceTests
    {
        [TestMethod]
        public void BuildNearbyMonsterBlockReason_JoinsOnlyTriggeredSegments()
        {
            string reason = LazyModeBlockerService.BuildNearbyMonsterBlockReason(
                nearbyNormalCount: 2,
                normalThreshold: 3,
                normalDistance: 40,
                normalTriggered: false,
                nearbyMagicCount: 2,
                magicThreshold: 2,
                magicDistance: 50,
                magicTriggered: true,
                nearbyRareCount: 1,
                rareThreshold: 1,
                rareDistance: 60,
                rareTriggered: true,
                nearbyUniqueCount: 0,
                uniqueThreshold: 1,
                uniqueDistance: 70,
                uniqueTriggered: false);

            reason.Should().Be("Magic 2/2 within 50, Rare 1/1 within 60");
        }

        [TestMethod]
        public void TryLogLazyModeRestriction_ThrottlesDuplicateReasons_WithinThrottleWindow()
        {
            var settings = new ClickItSettings();
            var logs = new List<string>();
            long now = 1_000;
            var service = new LazyModeBlockerService(settings, null, logs.Add, () => now);
            MethodInfo method = typeof(LazyModeBlockerService).GetMethod("TryLogLazyModeRestriction", BindingFlags.Instance | BindingFlags.NonPublic)!;

            method.Invoke(service, ["Nearby monster threshold reached"]);
            now += 100;
            method.Invoke(service, ["Nearby monster threshold reached"]);
            now += 600;
            method.Invoke(service, ["Nearby monster threshold reached"]);

            logs.Should().Equal(
                "Nearby monster threshold reached",
                "Nearby monster threshold reached");
        }

        [TestMethod]
        public void TryLogLazyModeRestriction_LogsChangedReason_EvenInsideThrottleWindow()
        {
            var settings = new ClickItSettings();
            var logs = new List<string>();
            long now = 2_000;
            var service = new LazyModeBlockerService(settings, null, logs.Add, () => now);
            MethodInfo method = typeof(LazyModeBlockerService).GetMethod("TryLogLazyModeRestriction", BindingFlags.Instance | BindingFlags.NonPublic)!;

            method.Invoke(service, ["Normal 1/1 within 40"]);
            now += 100;
            method.Invoke(service, ["Rare 1/1 within 60"]);

            logs.Should().Equal(
                "Normal 1/1 within 40",
                "Rare 1/1 within 60");
        }

        [TestMethod]
        public void HasRestrictedItemsOnScreen_ReturnsFalseAndClearsReason_WhenNoRestrictionsMatch()
        {
            var settings = new ClickItSettings();
            settings.LazyModeNormalMonsterBlockCount = 0;
            settings.LazyModeMagicMonsterBlockCount = 0;
            settings.LazyModeRareMonsterBlockCount = 0;
            settings.LazyModeUniqueMonsterBlockCount = 0;

            var service = new LazyModeBlockerService(settings, null, _ => { });
            IReadOnlyList<LabelOnGround> labels = [];

            bool result = service.HasRestrictedItemsOnScreen(labels);

            result.Should().BeFalse();
            service.LastRestrictionReason.Should().BeNull();
        }

        [TestMethod]
        public void HasRestrictedItemsOnScreen_ReturnsFalse_WhenLabelsAreNull()
        {
            var settings = new ClickItSettings();
            settings.LazyModeNormalMonsterBlockCount = 0;
            settings.LazyModeMagicMonsterBlockCount = 0;
            settings.LazyModeRareMonsterBlockCount = 0;
            settings.LazyModeUniqueMonsterBlockCount = 0;

            var service = new LazyModeBlockerService(settings, null, _ => { });

            bool result = service.HasRestrictedItemsOnScreen(null);

            result.Should().BeFalse();
            service.LastRestrictionReason.Should().BeNull();
        }

        [TestMethod]
        public void HasRestrictedItemsOnScreen_ReturnsTrueAndLogs_WhenCachedNearbyMonsterRestrictionIsFresh()
        {
            var settings = CreateNearbyMonsterSettings();
            var logs = new List<string>();
            long now = 1_000;
            var service = new LazyModeBlockerService(settings, null, logs.Add, () => now);
            string cachedReason = "Normal 1/1 within 40";

            SeedNearbyMonsterCache(service, settings, now, cachedResult: true, cachedReason);

            bool result = service.HasRestrictedItemsOnScreen([]);

            result.Should().BeTrue();
            service.LastRestrictionReason.Should().Be(cachedReason);
            logs.Should().Equal(cachedReason);
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_ReturnsCachedResult_WhenSignatureMatchesAndCacheIsFresh()
        {
            var settings = CreateNearbyMonsterSettings();
            long now = 2_000;
            var service = new LazyModeBlockerService(settings, null, _ => { }, () => now);

            SeedNearbyMonsterCache(service, settings, now, cachedResult: true, cachedReason: "Rare 1/1 within 60");

            (bool blocked, string? reason) = InvokeResolveNearbyMonsterRestriction(service);

            blocked.Should().BeTrue();
            reason.Should().Be("Rare 1/1 within 60");
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_ReturnsFalse_WhenRestrictionsEnabledButEntityListUnavailable()
        {
            var settings = CreateNearbyMonsterSettings();
            long now = 3_000;
            var service = new LazyModeBlockerService(settings, null, _ => { }, () => now);

            (bool blocked, string? reason) = InvokeResolveNearbyMonsterRestriction(service);

            blocked.Should().BeFalse();
            reason.Should().BeNull();
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_IgnoresExpiredCache_WhenEntityListUnavailable()
        {
            var settings = CreateNearbyMonsterSettings();
            long now = 4_000;
            var service = new LazyModeBlockerService(settings, null, _ => { }, () => now);

            SeedNearbyMonsterCache(service, settings, now - 100, cachedResult: true, cachedReason: "Cached result should expire");

            (bool blocked, string? reason) = InvokeResolveNearbyMonsterRestriction(service);

            blocked.Should().BeFalse();
            reason.Should().BeNull();
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_IgnoresCachedResult_WhenSettingsSignatureChanges()
        {
            var settings = CreateNearbyMonsterSettings();
            long now = 5_000;
            var service = new LazyModeBlockerService(settings, null, _ => { }, () => now);

            SeedNearbyMonsterCache(service, settings, now, cachedResult: true, cachedReason: "Signature should invalidate cache");
            settings.LazyModeUniqueMonsterBlockDistance += 5;

            (bool blocked, string? reason) = InvokeResolveNearbyMonsterRestriction(service);

            blocked.Should().BeFalse();
            reason.Should().BeNull();
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_ReturnsTrue_WhenLiveEntitiesReachMultipleThresholds()
        {
            var settings = CreateNearbyMonsterSettings();
            (bool blocked, string? reason) = LazyModeBlockerService.EvaluateNearbyMonsterRestriction(
            [
                new NearbyMonsterCandidate(true, EntityType.Monster, 35f, MonsterRarity.White, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 45f, MonsterRarity.Magic, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 50f, MonsterRarity.Magic, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 55f, MonsterRarity.Rare, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 65f, MonsterRarity.Unique, true, true)
            ],
                settings.LazyModeNormalMonsterBlockCount,
                settings.LazyModeNormalMonsterBlockDistance,
                settings.LazyModeMagicMonsterBlockCount,
                settings.LazyModeMagicMonsterBlockDistance,
                settings.LazyModeRareMonsterBlockCount,
                settings.LazyModeRareMonsterBlockDistance,
                settings.LazyModeUniqueMonsterBlockCount,
                settings.LazyModeUniqueMonsterBlockDistance);

            blocked.Should().BeTrue();
            reason.Should().Be("Normal 1/1 within 40, Magic 2/2 within 50, Rare 1/1 within 60, Unique 1/1 within 70");
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_IgnoresEntities_ThatCannotLegitimatelyBlockLazyMode()
        {
            var settings = CreateNearbyMonsterSettings();
            (bool blocked, string? reason) = LazyModeBlockerService.EvaluateNearbyMonsterRestriction(
            [
                new NearbyMonsterCandidate(false, EntityType.Monster, 35f, MonsterRarity.White, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, float.NaN, MonsterRarity.Magic, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, float.PositiveInfinity, MonsterRarity.Rare, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 80f, MonsterRarity.Unique, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 20f, MonsterRarity.White, false, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 20f, MonsterRarity.White, true, false),
                new NearbyMonsterCandidate(true, EntityType.Chest, 20f, MonsterRarity.White, true, true)
            ],
                settings.LazyModeNormalMonsterBlockCount,
                settings.LazyModeNormalMonsterBlockDistance,
                settings.LazyModeMagicMonsterBlockCount,
                settings.LazyModeMagicMonsterBlockDistance,
                settings.LazyModeRareMonsterBlockCount,
                settings.LazyModeRareMonsterBlockDistance,
                settings.LazyModeUniqueMonsterBlockCount,
                settings.LazyModeUniqueMonsterBlockDistance);

            blocked.Should().BeFalse();
            reason.Should().BeNull();
        }

        [TestMethod]
        public void TryFindLockedChestRestrictionReason_ReturnsReason_ForLockedNonStrongboxWithinDistance()
        {
            string? reason = LazyModeBlockerService.TryFindLockedChestRestrictionReason(
            [
                new LockedChestCandidate(12f, "Metadata/Chests/Standard", true, false)
            ],
                clickDistance: 50);

            reason.Should().Be("Locked chest detected (Metadata/Chests/Standard)");
        }

        [TestMethod]
        public void TryFindLockedChestRestrictionReason_IgnoresStrongboxes_EmptyPaths_AndDistantCandidates()
        {
            string? reason = LazyModeBlockerService.TryFindLockedChestRestrictionReason(
            [
                new LockedChestCandidate(12f, string.Empty, true, false),
                new LockedChestCandidate(12f, "Metadata/Chests/Strongbox", true, true),
                new LockedChestCandidate(60f, "Metadata/Chests/Distant", true, false),
                new LockedChestCandidate(10f, "Metadata/Chests/Unlocked", false, false)
            ],
                clickDistance: 50);

            reason.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateNearbyMonsterRestriction_ReturnsFalse_WhenAllThresholdsAreDisabled()
        {
            (bool blocked, string? reason) = LazyModeBlockerService.EvaluateNearbyMonsterRestriction(
            [
                new NearbyMonsterCandidate(true, EntityType.Monster, 10f, MonsterRarity.White, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 10f, MonsterRarity.Magic, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 10f, MonsterRarity.Rare, true, true),
                new NearbyMonsterCandidate(true, EntityType.Monster, 10f, MonsterRarity.Unique, true, true)
            ],
                normalThreshold: 0,
                normalDistance: 40,
                magicThreshold: 0,
                magicDistance: 50,
                rareThreshold: 0,
                rareDistance: 60,
                uniqueThreshold: 0,
                uniqueDistance: 70);

            blocked.Should().BeFalse();
            reason.Should().BeNull();
        }

        [DataTestMethod]
        [DataRow(MonsterRarity.White, 10f, 1, 20, 0, 0, 0, 0, "Normal 1/1 within 20")]
        [DataRow(MonsterRarity.Magic, 30f, 0, 0, 1, 30, 0, 0, "Magic 1/1 within 30")]
        [DataRow(MonsterRarity.Rare, 40f, 0, 0, 0, 0, 1, 40, "Rare 1/1 within 40")]
        [DataRow(MonsterRarity.Unique, 50f, 0, 0, 0, 0, 0, 0, "Unique 1/1 within 50")]
        public void EvaluateNearbyMonsterRestriction_BlocksWhenCandidateIsExactlyOnEnabledThreshold(
            MonsterRarity rarity,
            float distance,
            int normalThreshold,
            int normalDistance,
            int magicThreshold,
            int magicDistance,
            int rareThreshold,
            int rareDistance,
            string expectedReason)
        {
            int uniqueThreshold = rarity == MonsterRarity.Unique ? 1 : 0;
            int uniqueDistance = rarity == MonsterRarity.Unique ? (int)distance : 0;

            (bool blocked, string? reason) = LazyModeBlockerService.EvaluateNearbyMonsterRestriction(
            [
                new NearbyMonsterCandidate(true, EntityType.Monster, distance, rarity, true, true)
            ],
                normalThreshold,
                normalDistance,
                magicThreshold,
                magicDistance,
                rareThreshold,
                rareDistance,
                uniqueThreshold,
                uniqueDistance);

            blocked.Should().BeTrue();
            reason.Should().Be(expectedReason);
        }

        [TestMethod]
        public void ShouldBlockLazyModeForNearbyMonsters_RequiresEnabledThresholds()
        {
            bool blocked = LazyModeBlockerService.ShouldBlockLazyModeForNearbyMonsters(
                nearbyNormalCount: 10,
                normalThreshold: 0,
                nearbyMagicCount: 10,
                magicThreshold: 0,
                nearbyRareCount: 10,
                rareThreshold: 0,
                nearbyUniqueCount: 10,
                uniqueThreshold: 0);

            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldBlockLazyModeForNearbyMonsters_ReturnsTrue_WhenAnyEnabledThresholdIsMet()
        {
            bool blocked = LazyModeBlockerService.ShouldBlockLazyModeForNearbyMonsters(
                nearbyNormalCount: 0,
                normalThreshold: 1,
                nearbyMagicCount: 2,
                magicThreshold: 2,
                nearbyRareCount: 0,
                rareThreshold: 1,
                nearbyUniqueCount: 0,
                uniqueThreshold: 1);

            blocked.Should().BeTrue();
        }

        [DataTestMethod]
        [DataRow(true, 40, false, 50, false, 60, false, 70, 40)]
        [DataRow(false, 40, true, 50, true, 60, false, 70, 60)]
        [DataRow(false, 40, false, 50, false, 60, false, 70, 0)]
        public void GetMaxRelevantNearbyMonsterDistance_ReturnsExpectedMaximumEnabledDistance(
            bool normalEnabled,
            int normalDistance,
            bool magicEnabled,
            int magicDistance,
            bool rareEnabled,
            int rareDistance,
            bool uniqueEnabled,
            int uniqueDistance,
            int expected)
        {
            MethodInfo method = typeof(LazyModeBlockerService).GetMethod("GetMaxRelevantNearbyMonsterDistance", BindingFlags.Static | BindingFlags.NonPublic)!;

            int result = (int)method.Invoke(null,
            [
                normalEnabled,
                normalDistance,
                magicEnabled,
                magicDistance,
                rareEnabled,
                rareDistance,
                uniqueEnabled,
                uniqueDistance,
            ])!;

            result.Should().Be(expected);
        }

        private static ClickItSettings CreateNearbyMonsterSettings()
        {
            return new ClickItSettings
            {
                LazyModeNormalMonsterBlockCount = 1,
                LazyModeNormalMonsterBlockDistance = 40,
                LazyModeMagicMonsterBlockCount = 2,
                LazyModeMagicMonsterBlockDistance = 50,
                LazyModeRareMonsterBlockCount = 1,
                LazyModeRareMonsterBlockDistance = 60,
                LazyModeUniqueMonsterBlockCount = 1,
                LazyModeUniqueMonsterBlockDistance = 70,
            };
        }

        private static void SeedNearbyMonsterCache(
            LazyModeBlockerService service,
            ClickItSettings settings,
            long now,
            bool cachedResult,
            string? cachedReason)
        {
            int settingsSignature = HashCode.Combine(
                settings.LazyModeNormalMonsterBlockCount,
                settings.LazyModeNormalMonsterBlockDistance,
                settings.LazyModeMagicMonsterBlockCount,
                settings.LazyModeMagicMonsterBlockDistance,
                settings.LazyModeRareMonsterBlockCount,
                settings.LazyModeRareMonsterBlockDistance,
                settings.LazyModeUniqueMonsterBlockCount,
                settings.LazyModeUniqueMonsterBlockDistance);

            var cache = (TimedValueCache<int, LazyModeRestrictionResult>)RuntimeMemberAccessor.GetRequiredMemberValue(
                service,
                "_cachedNearbyMonsterRestrictionCacheState")!;
            cache.SetValue(
                settingsSignature,
                now - 10,
                new LazyModeRestrictionResult(cachedResult, cachedReason));
        }

        private static (bool Blocked, string? Reason) InvokeResolveNearbyMonsterRestriction(LazyModeBlockerService service)
        {
            MethodInfo method = typeof(LazyModeBlockerService).GetMethod("ResolveNearbyMonsterRestriction", BindingFlags.Instance | BindingFlags.NonPublic)!;
            LazyModeRestrictionResult restriction = (LazyModeRestrictionResult)method.Invoke(service, null)!;
            return (restriction.Blocked, restriction.Reason);
        }

    }
}