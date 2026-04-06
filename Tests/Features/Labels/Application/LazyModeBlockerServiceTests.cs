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

            (bool blocked, string? reason) = InvokeTryGetNearbyMonsterBlockReason(service);

            blocked.Should().BeTrue();
            reason.Should().Be("Rare 1/1 within 60");
        }

        [TestMethod]
        public void TryGetNearbyMonsterBlockReason_ReturnsFalse_WhenRestrictionsEnabledButEntityListUnavailable()
        {
            var settings = CreateNearbyMonsterSettings();
            long now = 3_000;
            var service = new LazyModeBlockerService(settings, null, _ => { }, () => now);

            (bool blocked, string? reason) = InvokeTryGetNearbyMonsterBlockReason(service);

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

            (bool blocked, string? reason) = InvokeTryGetNearbyMonsterBlockReason(service);

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

            (bool blocked, string? reason) = InvokeTryGetNearbyMonsterBlockReason(service);

            blocked.Should().BeFalse();
            reason.Should().BeNull();
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

            RuntimeMemberAccessor.SetRequiredMember(service, "_cachedNearbyMonsterRestrictionTimestampMs", now - 10);
            RuntimeMemberAccessor.SetRequiredMember(service, "_cachedNearbyMonsterRestrictionSettingsSignature", settingsSignature);
            RuntimeMemberAccessor.SetRequiredMember(service, "_cachedNearbyMonsterRestrictionResult", cachedResult);
            RuntimeMemberAccessor.SetRequiredMember(service, "_cachedNearbyMonsterRestrictionReason", cachedReason);
        }

        private static (bool Blocked, string? Reason) InvokeTryGetNearbyMonsterBlockReason(LazyModeBlockerService service)
        {
            MethodInfo method = typeof(LazyModeBlockerService).GetMethod("TryGetNearbyMonsterBlockReason", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object?[] arguments = [null];

            bool blocked = (bool)method.Invoke(service, arguments)!;

            return (blocked, (string?)arguments[0]);
        }

    }
}