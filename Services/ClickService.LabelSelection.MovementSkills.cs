using System.Windows.Forms;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        [ThreadStatic]
        internal static List<object?>? _threadSkillBarEntriesBuffer;

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

            if (remainingPathNodes < Math.Max(1, minPathNodes))
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
            ("ShieldCharge", new MovementSkillTimingProfile(MovementSkillShieldChargePostCastClickBlockMs, 0, true)),
            ("shield_charge", new MovementSkillTimingProfile(MovementSkillShieldChargePostCastClickBlockMs, 0, true)),
            ("LeapSlam", new MovementSkillTimingProfile(MovementSkillLeapSlamPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("leap_slam", new MovementSkillTimingProfile(MovementSkillLeapSlamPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("WhirlingBlades", new MovementSkillTimingProfile(MovementSkillWhirlingBladesPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("whirling_blades", new MovementSkillTimingProfile(MovementSkillWhirlingBladesPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("BlinkArrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("blink_arrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("MirrorArrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("mirror_arrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("ChargedDash", new MovementSkillTimingProfile(MovementSkillChargedDashPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("charged_dash", new MovementSkillTimingProfile(MovementSkillChargedDashPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("LightningWarp", new MovementSkillTimingProfile(MovementSkillLightningWarpPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("lightning_warp", new MovementSkillTimingProfile(MovementSkillLightningWarpPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("ConsecratedPath", new MovementSkillTimingProfile(MovementSkillConsecratedPathPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("consecrated_path", new MovementSkillTimingProfile(MovementSkillConsecratedPathPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("ChainHook", new MovementSkillTimingProfile(MovementSkillChainHookPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("chain_hook", new MovementSkillTimingProfile(MovementSkillChainHookPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false))
        ];

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

            return new MovementSkillTimingProfile(MovementSkillDefaultPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false);
        }

        private static bool ContainsSkillMarker(string skillName, string marker)
        {
            return skillName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
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
                if (normalized.IndexOf(MovementSkillInternalNameMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
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

            if (normalized.StartsWith("F", StringComparison.Ordinal) && int.TryParse(normalized[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            {
                key = Keys.F1 + (fNum - 1);
                return true;
            }

            return Enum.TryParse(normalized, ignoreCase: true, out key);
        }
    }
}