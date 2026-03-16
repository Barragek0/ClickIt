using System.Collections;
using System.Windows.Forms;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private bool TryUseMovementSkillForOffscreenPathing(string targetPath, Vector2 targetScreen, bool builtPath, out Vector2 castPoint, out string debugReason)
        {
            castPoint = default;
            debugReason = string.Empty;

            int remainingNodes = GetRemainingOffscreenPathNodeCount();
            int minimumNodes = Math.Max(1, settings.OffscreenMovementSkillMinPathSubsectionLength?.Value ?? 8);
            long now = Environment.TickCount64;
            bool movementSkillsEnabled = settings.UseMovementSkillsForOffscreenPathfinding?.Value == true;

            if (!movementSkillsEnabled)
            {
                debugReason = "Skipped: setting disabled (Use Movement Skills for Offscreen Pathfinding = false).";
                return false;
            }

            if (!builtPath)
            {
                debugReason = "Skipped: no fresh path available (movement skill requires successful path build).";
                return false;
            }

            if (remainingNodes < minimumNodes)
            {
                debugReason = $"Skipped: remaining path nodes {remainingNodes} below minimum {minimumNodes}.";
                return false;
            }

            if (_lastMovementSkillUseTimestampMs > 0 && MovementSkillRecastDelayMs > 0)
            {
                long elapsed = now - _lastMovementSkillUseTimestampMs;
                if (elapsed < MovementSkillRecastDelayMs)
                {
                    debugReason = $"Skipped: local recast delay active ({elapsed}ms elapsed, need {MovementSkillRecastDelayMs}ms).";
                    return false;
                }
            }

            if (!ShouldAttemptMovementSkill(
                movementSkillsEnabled,
                builtPath,
                remainingPathNodes: remainingNodes,
                minPathNodes: minimumNodes,
                now,
                lastSkillUseTimestampMs: _lastMovementSkillUseTimestampMs,
                recastDelayMs: MovementSkillRecastDelayMs))
            {
                debugReason = "Skipped: movement skill gate returned false.";
                return false;
            }

            if (!TryResolveMovementSkillCastPosition(targetScreen, targetPath, out castPoint))
            {
                debugReason = "Skipped: unable to resolve safe/clickable movement-skill cast point.";
                return false;
            }

            if (!TryFindReadyMovementSkillKey(out Keys boundKey, out string movementSkillName, out object? movementSkillEntry, out string skillSearchDebug))
            {
                debugReason = $"Skipped: no ready movement skill key found. {skillSearchDebug}";
                return false;
            }

            if (!EnsureCursorInsideGameWindowForClick("[TryUseMovementSkillForOffscreenPathing] Skipping cast - cursor outside PoE window"))
            {
                debugReason = "Skipped: cursor outside game window safety check failed.";
                return false;
            }

            if (!Mouse.DisableNativeInput)
            {
                Input.SetCursorPos(castPoint);
                Thread.Sleep(10);
            }

            Keyboard.KeyPress(boundKey, MovementSkillKeyTapDelayMs);
            _lastMovementSkillUseTimestampMs = now;
            int postCastClickBlockMs = ResolveMovementSkillPostCastClickBlockMsForCast(movementSkillName);
            _movementSkillPostCastClickBlockUntilTimestampMs = postCastClickBlockMs > 0
                ? now + postCastClickBlockMs
                : 0;
            int statusPollWindowMs = ResolveMovementSkillStatusPollWindowMs(postCastClickBlockMs, movementSkillName);
            _movementSkillStatusPollUntilTimestampMs = statusPollWindowMs > 0
                ? now + statusPollWindowMs
                : 0;
            _lastUsedMovementSkillEntry = statusPollWindowMs > 0
                ? movementSkillEntry
                : null;
            performanceMonitor.RecordClickInterval();
            DebugLog(() => $"[TryUseMovementSkillForOffscreenPathing] Cast movement skill '{movementSkillName}' with key '{boundKey}'");
            debugReason = $"Used movement skill '{movementSkillName}' with key '{boundKey}' (remainingNodes={remainingNodes}, minNodes={minimumNodes}, postCastClickBlockMs={postCastClickBlockMs}, statusPollWindowMs={statusPollWindowMs}).";
            return true;
        }

        private int ResolveMovementSkillPostCastClickBlockMsForCast(string? movementSkillInternalName)
        {
            int resolved = ResolveMovementSkillPostCastClickBlockMs(movementSkillInternalName);
            if (!IsShieldChargeMovementSkill(movementSkillInternalName))
                return resolved;

            return Math.Max(0, settings.OffscreenShieldChargePostCastClickDelayMs?.Value ?? MovementSkillShieldChargePostCastClickBlockMs);
        }

        private bool TryGetMovementSkillPostCastBlockState(long now, out string reason)
        {
            reason = string.Empty;

            if (IsMovementSkillPostCastClickBlocked(now, _movementSkillPostCastClickBlockUntilTimestampMs, out long remainingMs))
            {
                reason = $"timing window active ({remainingMs}ms remaining)";
                return true;
            }

            if (_movementSkillStatusPollUntilTimestampMs <= 0 || now > _movementSkillStatusPollUntilTimestampMs)
            {
                _movementSkillStatusPollUntilTimestampMs = 0;
                _lastUsedMovementSkillEntry = null;
                return false;
            }

            if (!TryResolveMovementSkillRuntimeState(_lastUsedMovementSkillEntry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed))
                return false;

            if (isUsing)
            {
                reason = "Skill.IsUsing=true";
                return true;
            }

            if (allowedToCast.HasValue && !allowedToCast.Value)
            {
                reason = "Skill.AllowedToCast=false";
                return true;
            }

            if (canBeUsed.HasValue && !canBeUsed.Value)
            {
                reason = "Skill.CanBeUsed=false";
                return true;
            }

            _movementSkillStatusPollUntilTimestampMs = 0;
            _lastUsedMovementSkillEntry = null;
            return false;
        }

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

        private static bool TryResolveMovementSkillRuntimeState(object? entry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed)
        {
            isUsing = false;
            allowedToCast = null;
            canBeUsed = null;

            if (entry == null)
                return false;

            object skillObject = ResolveSkillObject(entry);

            bool foundAny = false;

            if (TryReadBoolSkillMember(skillObject, entry, out bool usingValue, s => s.IsUsing))
            {
                isUsing = usingValue;
                foundAny = true;
            }

            if (TryReadBoolSkillMember(skillObject, entry, out bool allowedValue, s => s.AllowedToCast))
            {
                allowedToCast = allowedValue;
                foundAny = true;
            }

            if (TryReadBoolSkillMember(skillObject, entry, out bool canUseValue, s => s.CanBeUsed))
            {
                canBeUsed = canUseValue;
                foundAny = true;
            }

            return foundAny;
        }

        private static object ResolveSkillObject(object entry)
        {
            if (TryGetDynamicValue(entry, s => s.Skill, out object? skill) && skill != null)
                return skill;

            if (TryGetDynamicValue(entry, s => s.ActorSkill, out object? actorSkill) && actorSkill != null)
                return actorSkill;

            return entry;
        }

        private static bool TryReadBoolSkillMember(object skillObject, object entry, out bool value, Func<dynamic, object?> accessor)
        {
            value = false;

            if (TryReadBool(accessor, skillObject, out value))
                return true;

            return TryReadBool(accessor, entry, out value);
        }

        private static bool TryReadBool(Func<dynamic, object?> accessor, object? source, out bool value)
        {
            return DynamicAccess.TryReadBool(source, accessor, out value);
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

        private static bool IsShieldChargeMovementSkill(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return false;

            return ContainsSkillMarker(movementSkillInternalName, "ShieldCharge")
                || ContainsSkillMarker(movementSkillInternalName, "shield_charge");
        }

        private bool TryResolveMovementSkillCastPosition(Vector2 targetScreen, string targetPath, out Vector2 castPoint)
        {
            castPoint = default;

            RectangleF win = gameController.Window.GetWindowRectangleTimeCache;
            if (win.Width <= 0 || win.Height <= 0)
                return false;

            float insetX = Math.Max(24f, win.Width * 0.12f);
            float insetY = Math.Max(24f, win.Height * 0.12f);
            float safeLeft = win.Left + insetX;
            float safeRight = win.Right - insetX;
            float safeTop = win.Top + insetY;
            float safeBottom = win.Bottom - insetY;

            Vector2 center = new Vector2(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            for (float t = 1.65f; t >= 0.70f; t -= 0.1f)
            {
                Vector2 candidate = center + (direction * t);
                if (!IsInsideWindow(win, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                castPoint = candidate;
                return true;
            }

            Vector2 clamped = new Vector2(
                Math.Clamp(targetScreen.X, safeLeft, safeRight),
                Math.Clamp(targetScreen.Y, safeTop, safeBottom));

            if (pointIsInClickableArea(clamped, targetPath))
            {
                castPoint = clamped;
                return true;
            }

            return false;
        }

        private bool TryFindReadyMovementSkillKey(out Keys boundKey, out string movementSkillName, out object? matchedSkillEntry, out string diagnostic)
        {
            boundKey = Keys.None;
            movementSkillName = string.Empty;
            matchedSkillEntry = null;
            diagnostic = string.Empty;

            if (!TryGetSkillBarEntries(out IReadOnlyList<object?> skillEntries))
            {
                diagnostic = "SkillBar/Skills collection unavailable.";
                return false;
            }

            if (skillEntries.Count == 0)
            {
                diagnostic = "SkillBar contains zero entries.";
                return false;
            }

            int nullEntries = 0;
            int nonMovementEntries = 0;
            int cooldownEntries = 0;
            int missingKeyEntries = 0;
            int unsupportedKeyEntries = 0;

            for (int i = 0; i < skillEntries.Count; i++)
            {
                object? entry = skillEntries[i];
                if (entry == null)
                {
                    nullEntries++;
                    continue;
                }

                if (!TryResolveMovementSkillInternalName(entry, out string internalName))
                {
                    nonMovementEntries++;
                    continue;
                }

                if (IsSkillEntryOnCooldown(entry))
                {
                    cooldownEntries++;
                    continue;
                }

                if (!TryResolveSkillKeyText(entry, out string keyText))
                {
                    missingKeyEntries++;
                    continue;
                }

                if (!TryMapKeyTextToKeys(keyText, out Keys parsedKey))
                {
                    unsupportedKeyEntries++;
                    continue;
                }

                boundKey = parsedKey;
                movementSkillName = internalName;
                matchedSkillEntry = entry;
                diagnostic = $"Matched movement skill '{internalName}' on key text '{keyText}'.";
                return true;
            }

            diagnostic = $"entries={skillEntries.Count}, null={nullEntries}, nonMovement={nonMovementEntries}, onCooldown={cooldownEntries}, missingKeyText={missingKeyEntries}, unsupportedOrMouseKey={unsupportedKeyEntries}";
            return false;
        }

        private bool TryGetSkillBarEntries(out IReadOnlyList<object?> entries)
        {
            entries = [];

            object? skillBar = gameController?.IngameState?.IngameUi?.SkillBar;
            if (skillBar == null)
                return false;

            if (!TryGetDynamicValue(skillBar, s => s.Skills, out object? skillsCollection))
                return false;

            if (skillsCollection is not IEnumerable enumerable)
                return false;

            var list = new List<object?>();
            foreach (object? entry in enumerable)
            {
                list.Add(entry);
            }

            entries = list;
            return entries.Count > 0;
        }

        private bool TryResolveMovementSkillInternalName(object entry, out string internalName)
        {
            internalName = string.Empty;

            object skillObject = ResolveSkillObject(entry);

            if (TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.InternalName)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.Name)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.Id)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.SkillId)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.MetadataId))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveMovementSkillNameCandidate(object skillObject, object entry, out string internalName, Func<dynamic, object?> accessor)
        {
            internalName = string.Empty;

            if (!TryReadString(accessor, skillObject, out string candidate)
                && !TryReadString(accessor, entry, out candidate))
            {
                return false;
            }

            if (!IsMovementSkillInternalName(candidate))
                return false;

            internalName = candidate;
            return true;
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

        private static bool IsSkillEntryOnCooldown(object entry)
        {
            object skillObject = ResolveSkillObject(entry);
            return TryReadBoolSkillMember(skillObject, entry, out bool onCooldown, s => s.IsOnCooldown)
                ? onCooldown
                : TryReadBoolSkillMember(skillObject, entry, out onCooldown, s => s.OnCooldown)
                    ? onCooldown
                    : TryReadBoolSkillMember(skillObject, entry, out onCooldown, s => s.HasCooldown) && onCooldown;
        }

        private static bool TryResolveSkillKeyText(object entry, out string keyText)
        {
            keyText = string.Empty;

            object skillObject = ResolveSkillObject(entry);

            if (TryReadStringSkillMember(skillObject, entry, out keyText, s => s.KeyText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.SkillBarText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Key)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Bind)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Hotkey)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.InputText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.SlotText))
            {
                return true;
            }

            if (TryResolveSkillKeyTextFromKnownChildPath(entry, out string childPathText))
            {
                keyText = childPathText;
                return true;
            }

            return false;
        }

        private static bool TryResolveSkillKeyTextFromKnownChildPath(object entry, out string keyText)
        {
            keyText = string.Empty;

            object? node = entry;
            int[] childIndices = [0, 0, 0, 1];
            for (int i = 0; i < childIndices.Length; i++)
            {
                if (!TryGetChildNode(node, childIndices[i], out node) || node == null)
                    return false;
            }

            return TryReadNodeText(node, out keyText);
        }

        private static bool TryGetChildNode(object? node, int index, out object? child)
        {
            child = null;
            if (node == null || index < 0)
                return false;

            if (node is Element element)
            {
                child = element.GetChildAtIndex(index);
                return child != null;
            }

            if (TryGetDynamicValue(node, n => n.Child(index), out object? dynamicChild) && dynamicChild != null)
            {
                child = dynamicChild;
                return true;
            }

            if (TryGetDynamicValue(node, n => n.Children, out object? childrenObj) && childrenObj is IList list && index < list.Count)
            {
                child = list[index];
                return child != null;
            }

            return false;
        }

        private static bool TryReadNodeText(object node, out string text)
        {
            text = string.Empty;
            if (node == null)
                return false;

            if (node is Element element)
            {
                string value = element.GetText(256) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    text = value.Trim();
                    return true;
                }
            }

            return TryReadString(n => n.Text, node, out text)
                || TryReadString(n => n.Label, node, out text)
                || TryReadString(n => n.KeyText, node, out text);
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

        private static bool TryReadStringSkillMember(object skillObject, object entry, out string value, Func<dynamic, object?> accessor)
        {
            value = string.Empty;

            if (TryReadString(accessor, entry, out value))
                return true;

            return TryReadString(accessor, skillObject, out value);
        }

        private static bool TryReadString(Func<dynamic, object?> accessor, object? source, out string value)
        {
            return DynamicAccess.TryReadString(source, accessor, out value);
        }

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            return DynamicAccess.TryGetDynamicValue(source, accessor, out value);
        }
    }
}