using ExileCore;
using System.Collections;
using System.Windows.Forms;
using ClickIt.Utils;
using SharpDX;
using System.Threading;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    internal readonly record struct MovementSkillCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        PerformanceMonitor PerformanceMonitor,
        Func<int> GetRemainingOffscreenPathNodeCount,
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Action<string> DebugLog,
        Func<long> GetLastMovementSkillUseTimestampMs,
        Action<long> SetLastMovementSkillUseTimestampMs,
        Func<long> GetMovementSkillPostCastClickBlockUntilTimestampMs,
        Action<long> SetMovementSkillPostCastClickBlockUntilTimestampMs,
        Func<long> GetMovementSkillStatusPollUntilTimestampMs,
        Action<long> SetMovementSkillStatusPollUntilTimestampMs,
        Func<object?> GetLastUsedMovementSkillEntry,
        Action<object?> SetLastUsedMovementSkillEntry);

    internal sealed class MovementSkillCoordinator(MovementSkillCoordinatorDependencies dependencies)
    {
        private readonly MovementSkillCoordinatorDependencies _dependencies = dependencies;

        public bool TryUseMovementSkillForOffscreenPathing(string targetPath, Vector2 targetScreen, bool builtPath, out Vector2 castPoint, out string debugReason)
        {
            castPoint = default;
            debugReason = string.Empty;

            int remainingNodes = _dependencies.GetRemainingOffscreenPathNodeCount();
            int minimumNodes = Math.Max(1, _dependencies.Settings.OffscreenMovementSkillMinPathSubsectionLength?.Value ?? 8);
            long now = Environment.TickCount64;
            bool movementSkillsEnabled = _dependencies.Settings.UseMovementSkillsForOffscreenPathfinding?.Value == true;
            long lastSkillUseTimestampMs = _dependencies.GetLastMovementSkillUseTimestampMs();

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

            if (lastSkillUseTimestampMs > 0 && ClickService.MovementSkillRecastDelayMs > 0)
            {
                long elapsed = now - lastSkillUseTimestampMs;
                if (elapsed < ClickService.MovementSkillRecastDelayMs)
                {
                    debugReason = $"Skipped: local recast delay active ({elapsed}ms elapsed, need {ClickService.MovementSkillRecastDelayMs}ms).";
                    return false;
                }
            }

            if (!ClickService.ShouldAttemptMovementSkill(
                movementSkillsEnabled,
                builtPath,
                remainingNodes,
                minimumNodes,
                now,
                lastSkillUseTimestampMs,
                ClickService.MovementSkillRecastDelayMs))
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

            if (!_dependencies.EnsureCursorInsideGameWindowForClick("[TryUseMovementSkillForOffscreenPathing] Skipping cast - cursor outside PoE window"))
            {
                debugReason = "Skipped: cursor outside game window safety check failed.";
                return false;
            }

            if (!Mouse.DisableNativeInput)
            {
                Input.SetCursorPos(castPoint);
                Thread.Sleep(10);
            }

            Keyboard.KeyPress(boundKey, ClickService.MovementSkillKeyTapDelayMs);
            _dependencies.SetLastMovementSkillUseTimestampMs(now);
            int postCastClickBlockMs = ResolveMovementSkillPostCastClickBlockMsForCast(movementSkillName);
            _dependencies.SetMovementSkillPostCastClickBlockUntilTimestampMs(postCastClickBlockMs > 0 ? now + postCastClickBlockMs : 0);
            int statusPollWindowMs = ClickService.ResolveMovementSkillStatusPollWindowMs(postCastClickBlockMs, movementSkillName);
            _dependencies.SetMovementSkillStatusPollUntilTimestampMs(statusPollWindowMs > 0 ? now + statusPollWindowMs : 0);
            _dependencies.SetLastUsedMovementSkillEntry(statusPollWindowMs > 0 ? movementSkillEntry : null);
            _dependencies.PerformanceMonitor.RecordClickInterval();
            _dependencies.DebugLog($"[TryUseMovementSkillForOffscreenPathing] Cast movement skill '{movementSkillName}' with key '{boundKey}'");
            debugReason = $"Used movement skill '{movementSkillName}' with key '{boundKey}' (remainingNodes={remainingNodes}, minNodes={minimumNodes}, postCastClickBlockMs={postCastClickBlockMs}, statusPollWindowMs={statusPollWindowMs}).";
            return true;
        }

        public bool TryGetMovementSkillPostCastBlockState(long now, out string reason)
        {
            reason = string.Empty;

            if (ClickService.IsMovementSkillPostCastClickBlocked(now, _dependencies.GetMovementSkillPostCastClickBlockUntilTimestampMs(), out long remainingMs))
            {
                reason = $"timing window active ({remainingMs}ms remaining)";
                return true;
            }

            long statusPollUntilTimestampMs = _dependencies.GetMovementSkillStatusPollUntilTimestampMs();
            if (statusPollUntilTimestampMs <= 0 || now > statusPollUntilTimestampMs)
            {
                _dependencies.SetMovementSkillStatusPollUntilTimestampMs(0);
                _dependencies.SetLastUsedMovementSkillEntry(null);
                return false;
            }

            if (!TryResolveMovementSkillRuntimeState(_dependencies.GetLastUsedMovementSkillEntry(), out bool isUsing, out bool? allowedToCast, out bool? canBeUsed))
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

            _dependencies.SetMovementSkillStatusPollUntilTimestampMs(0);
            _dependencies.SetLastUsedMovementSkillEntry(null);
            return false;
        }

        public int ResolveMovementSkillPostCastClickBlockMsForCast(string? movementSkillInternalName)
        {
            int resolved = ClickService.ResolveMovementSkillPostCastClickBlockMs(movementSkillInternalName);
            if (!ClickService.IsShieldChargeMovementSkill(movementSkillInternalName))
                return resolved;

            return Math.Max(0, _dependencies.Settings.OffscreenShieldChargePostCastClickDelayMs?.Value ?? ClickService.MovementSkillShieldChargePostCastClickBlockMs);
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
            return DynamicObjectAdapter.TryReadBoolFromEither(skillObject, entry, accessor, out value);
        }

        private bool TryResolveMovementSkillCastPosition(Vector2 targetScreen, string targetPath, out Vector2 castPoint)
        {
            castPoint = default;

            RectangleF win = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            if (win.Width <= 0 || win.Height <= 0)
                return false;

            float insetX = Math.Max(24f, win.Width * 0.12f);
            float insetY = Math.Max(24f, win.Height * 0.12f);
            float safeLeft = win.Left + insetX;
            float safeRight = win.Right - insetX;
            float safeTop = win.Top + insetY;
            float safeBottom = win.Bottom - insetY;

            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            for (float t = 1.65f; t >= 0.70f; t -= 0.1f)
            {
                Vector2 candidate = center + (direction * t);
                if (!ClickService.IsInsideWindow(win, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!_dependencies.PointIsInClickableArea(candidate, targetPath))
                    continue;

                castPoint = candidate;
                return true;
            }

            Vector2 clamped = new(
                Math.Clamp(targetScreen.X, safeLeft, safeRight),
                Math.Clamp(targetScreen.Y, safeTop, safeBottom));

            if (_dependencies.PointIsInClickableArea(clamped, targetPath))
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

                if (!ClickService.TryMapKeyTextToKeys(keyText, out Keys parsedKey))
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

            object? skillBar = _dependencies.GameController?.IngameState?.IngameUi?.SkillBar;
            if (skillBar == null)
                return false;

            if (!TryGetDynamicValue(skillBar, s => s.Skills, out object? skillsCollection))
                return false;

            if (skillsCollection is not IEnumerable enumerable)
                return false;

            List<object?> list = ClickService._threadSkillBarEntriesBuffer ??= new List<object?>(16);
            list.Clear();
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

            if (!ClickService.IsMovementSkillInternalName(candidate))
                return false;

            internalName = candidate;
            return true;
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

            if (TryGetDynamicValue(node, n => n.GetChildAtIndex(index), out object? directChild) && directChild != null)
            {
                child = directChild;
                return true;
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

            return TryReadString(n => n.GetText(256), node, out text)
                || TryReadString(n => n.Text, node, out text)
                || TryReadString(n => n.Label, node, out text)
                || TryReadString(n => n.KeyText, node, out text);
        }

        private static bool TryReadStringSkillMember(object skillObject, object entry, out string value, Func<dynamic, object?> accessor)
        {
            return DynamicObjectAdapter.TryReadStringFromEither(entry, skillObject, accessor, out value);
        }

        private static bool TryReadString(Func<dynamic, object?> accessor, object? source, out string value)
        {
            return DynamicObjectAdapter.TryReadString(source, accessor, out value);
        }

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            return DynamicObjectAdapter.TryGetValue(source, accessor, out value);
        }
    }
}