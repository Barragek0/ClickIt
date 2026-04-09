namespace ClickIt.Features.Click.Runtime
{
    internal static class MovementSkillMath
    {
        internal const int RecastDelayMs = 450;
        internal const int KeyTapDelayMs = 30;
        internal const int ShieldChargePostCastClickBlockMs = 100;
        private const int DefaultPostCastClickBlockMs = 120;
        private const int LeapSlamPostCastClickBlockMs = 230;
        private const int WhirlingBladesPostCastClickBlockMs = 170;
        private const int BlinkArrowPostCastClickBlockMs = 260;
        private const int ChargedDashPostCastClickBlockMs = 300;
        private const int LightningWarpPostCastClickBlockMs = 320;
        private const int ConsecratedPathPostCastClickBlockMs = 240;
        private const int ChainHookPostCastClickBlockMs = 220;
        private const int DefaultStatusPollExtraMs = 900;
        private const int ExtendedStatusPollExtraMs = 1300;

        private static readonly string[] MovementSkillInternalNameMarkers =
        [
            "QuickDashGem",
            "Dash",
            "dash",
            "FlameDash",
            "flame_dash",
            "FrostblinkSkillGem",
            "Frostblink",
            "frostblink",
            "LeapSlam",
            "leap_slam",
            "ShieldCharge",
            "shield_charge",
            "WhirlingBlades",
            "whirling_blades",
            "BlinkArrow",
            "blink_arrow",
            "MirrorArrow",
            "mirror_arrow",
            "CorpseWarp",
            "Bodyswap",
            "bodyswap",
            "LightningWarp",
            "lightning_warp",
            "ChargedDashGem",
            "ChargedDash",
            "charged_dash",
            "HolyPathGem",
            "ConsecratedPath",
            "consecrated_path",
            "PhaseRun",
            "phase_run",
            "ChainStrikeGem",
            "ChainHook",
            "chain_hook",
            "WitheringStepGem",
            "WitheringStep",
            "withering_step",
            "SmokeBomb",
            "SmokeMine",
            "smoke_mine",
            "AmbushSkillGem",
            "Ambush",
            "ambush_player",
            "QuickStepGem",
            "slow_dodge"
        ];

        [ThreadStatic]
        private static List<object?>? _threadSkillBarEntriesBuffer;

        private readonly record struct MovementSkillTimingProfile(int PostCastClickBlockMs, int StatusPollExtraMs, bool DisableStatusPoll);

        private static readonly (string Marker, MovementSkillTimingProfile Profile)[] MovementSkillTimingProfiles =
        [
            ("Frostblink", new MovementSkillTimingProfile(0, 0, true)),
            ("QuickDashGem", new MovementSkillTimingProfile(0, 0, true)),
            ("Dash", new MovementSkillTimingProfile(0, 0, true)),
            ("FlameDash", new MovementSkillTimingProfile(0, 0, true)),
            ("flame_dash", new MovementSkillTimingProfile(0, 0, true)),
            ("WitheringStep", new MovementSkillTimingProfile(0, 0, true)),
            ("withering_step", new MovementSkillTimingProfile(0, 0, true)),
            ("PhaseRun", new MovementSkillTimingProfile(0, 0, true)),
            ("phase_run", new MovementSkillTimingProfile(0, 0, true)),
            ("Ambush", new MovementSkillTimingProfile(0, 0, true)),
            ("ambush_player", new MovementSkillTimingProfile(0, 0, true)),
            ("ShieldCharge", new MovementSkillTimingProfile(ShieldChargePostCastClickBlockMs, 0, true)),
            ("shield_charge", new MovementSkillTimingProfile(ShieldChargePostCastClickBlockMs, 0, true)),
            ("LeapSlam", new MovementSkillTimingProfile(LeapSlamPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("leap_slam", new MovementSkillTimingProfile(LeapSlamPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("WhirlingBlades", new MovementSkillTimingProfile(WhirlingBladesPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("whirling_blades", new MovementSkillTimingProfile(WhirlingBladesPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("BlinkArrow", new MovementSkillTimingProfile(BlinkArrowPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("blink_arrow", new MovementSkillTimingProfile(BlinkArrowPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("MirrorArrow", new MovementSkillTimingProfile(BlinkArrowPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("mirror_arrow", new MovementSkillTimingProfile(BlinkArrowPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("ChargedDash", new MovementSkillTimingProfile(ChargedDashPostCastClickBlockMs, ExtendedStatusPollExtraMs, false)),
            ("charged_dash", new MovementSkillTimingProfile(ChargedDashPostCastClickBlockMs, ExtendedStatusPollExtraMs, false)),
            ("LightningWarp", new MovementSkillTimingProfile(LightningWarpPostCastClickBlockMs, ExtendedStatusPollExtraMs, false)),
            ("lightning_warp", new MovementSkillTimingProfile(LightningWarpPostCastClickBlockMs, ExtendedStatusPollExtraMs, false)),
            ("ConsecratedPath", new MovementSkillTimingProfile(ConsecratedPathPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("consecrated_path", new MovementSkillTimingProfile(ConsecratedPathPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("ChainHook", new MovementSkillTimingProfile(ChainHookPostCastClickBlockMs, DefaultStatusPollExtraMs, false)),
            ("chain_hook", new MovementSkillTimingProfile(ChainHookPostCastClickBlockMs, DefaultStatusPollExtraMs, false))
        ];

        internal static bool ShouldAttemptMovementSkill(
            bool movementSkillsEnabled,
            bool builtPath,
            int remainingPathNodes,
            int minPathNodes,
            long now,
            long lastSkillUseTimestampMs,
            int recastDelayMs)
        {
            if (!movementSkillsEnabled || !builtPath)
                return false;

            if (remainingPathNodes < SystemMath.Max(1, minPathNodes))
                return false;

            if (lastSkillUseTimestampMs <= 0 || recastDelayMs <= 0)
                return true;

            long elapsed = now - lastSkillUseTimestampMs;
            return elapsed >= recastDelayMs;
        }

        internal static bool IsMovementSkillPostCastClickBlocked(long now, long blockUntilTimestampMs, out long remainingMs)
        {
            remainingMs = 0;
            if (blockUntilTimestampMs <= 0)
                return false;

            long remaining = blockUntilTimestampMs - now;
            if (remaining <= 0)
                return false;

            remainingMs = remaining;
            return true;
        }

        internal static int ResolveMovementSkillStatusPollWindowMs(int postCastClickBlockMs, string? movementSkillInternalName)
        {
            if (postCastClickBlockMs <= 0 || string.IsNullOrWhiteSpace(movementSkillInternalName))
                return 0;

            MovementSkillTimingProfile profile = ResolveMovementSkillTimingProfile(movementSkillInternalName);
            if (profile.DisableStatusPoll)
                return 0;

            return postCastClickBlockMs + profile.StatusPollExtraMs;
        }

        internal static int ResolveMovementSkillPostCastClickBlockMs(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return 0;

            MovementSkillTimingProfile profile = ResolveMovementSkillTimingProfile(movementSkillInternalName);
            return profile.PostCastClickBlockMs;
        }

        internal static bool IsShieldChargeMovementSkill(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return false;

            return ContainsSkillMarker(movementSkillInternalName, "ShieldCharge")
                || ContainsSkillMarker(movementSkillInternalName, "shield_charge");
        }

        internal static bool IsMovementSkillInternalName(string? skillInternalName)
        {
            if (string.IsNullOrWhiteSpace(skillInternalName))
                return false;

            string normalized = skillInternalName.Trim();
            for (int i = 0; i < MovementSkillInternalNameMarkers.Length; i++)
            {
                if (normalized.Contains(MovementSkillInternalNameMarkers[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static bool TryMapKeyTextToKeys(string? keyText, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(keyText))
                return false;

            string normalized = keyText.Trim().ToUpperInvariant();
            string[] modifierSplit = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (modifierSplit.Length > 0)
                normalized = modifierSplit[modifierSplit.Length - 1];

            normalized = normalized.Replace(" ", string.Empty);

            if (normalized is "LMB" or "RMB" or "MMB" or "MOUSE4" or "MOUSE5")
                return false;

            if (normalized.Length == 1)
            {
                char c = normalized[0];
                if (c >= 'A' && c <= 'Z')
                {
                    key = Keys.A + (c - 'A');
                    return true;
                }

                if (c >= '0' && c <= '9')
                {
                    key = Keys.D0 + (c - '0');
                    return true;
                }
            }

            if (normalized.StartsWith('F') && int.TryParse(normalized[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            {
                key = Keys.F1 + (fNum - 1);
                return true;
            }

            return Enum.TryParse(normalized, ignoreCase: true, out key);
        }

        internal static List<object?> GetThreadSkillBarEntriesBuffer(int capacity)
            => _threadSkillBarEntriesBuffer ??= new List<object?>(SystemMath.Max(1, capacity));

        internal static void ClearThreadSkillBarEntriesBuffer()
        {
            _threadSkillBarEntriesBuffer?.Clear();
            _threadSkillBarEntriesBuffer = null;
        }

        private static MovementSkillTimingProfile ResolveMovementSkillTimingProfile(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return new MovementSkillTimingProfile(0, 0, true);

            string normalized = movementSkillInternalName.Trim();
            for (int i = 0; i < MovementSkillTimingProfiles.Length; i++)
            {
                (string marker, MovementSkillTimingProfile profile) = MovementSkillTimingProfiles[i];
                if (ContainsSkillMarker(normalized, marker))
                    return profile;
            }

            return new MovementSkillTimingProfile(DefaultPostCastClickBlockMs, DefaultStatusPollExtraMs, false);
        }

        private static bool ContainsSkillMarker(string skillName, string marker)
            => skillName.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }
}