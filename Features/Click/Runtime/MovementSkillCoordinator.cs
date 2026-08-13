namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct MovementSkillCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ClickRuntimeState RuntimeState,
        PerformanceMonitor PerformanceMonitor,
        Func<int> GetRemainingOffscreenPathNodeCount,
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Action<string> DebugLog,
        Func<long>? GetTimestampMs = null);

    internal sealed class MovementSkillCoordinator(MovementSkillCoordinatorDependencies dependencies)
    {
        private readonly MovementSkillCoordinatorDependencies _dependencies = dependencies;
        private readonly Func<long> _getTimestampMs = dependencies.GetTimestampMs ?? (static () => Environment.TickCount64);

        // The skill may not be cast until ~200ms have elapsed since pathfinding to the CURRENT target started (so the mouse moves to the correct direction first). Pathfinding and walk clicks continue during the delay - only the cast is gated. A new target restarts the clock.
        private string? _castDelayTargetPath;
        private long _castDelayStartTimestampMs;

        public bool TryUseMovementSkillForOffscreenPathing(string targetPath, Vector2 targetScreen, bool builtPath, out Vector2 castPoint, out string debugReason)
        {
            castPoint = default;
            debugReason = string.Empty;

            int remainingNodes = _dependencies.GetRemainingOffscreenPathNodeCount();
            int minimumNodes = SystemMath.Max(1, _dependencies.Settings.OffscreenMovementSkillMinPathSubsectionLength?.Value ?? 8);
            long now = _getTimestampMs();
            bool movementSkillsEnabled = _dependencies.Settings.UseMovementSkillsForOffscreenPathfinding?.Value == true;
            long lastSkillUseTimestampMs = _dependencies.RuntimeState.LastMovementSkillUseTimestampMs;

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

            if (lastSkillUseTimestampMs > 0 && MovementSkillMath.RecastDelayMs > 0)
            {
                long elapsed = now - lastSkillUseTimestampMs;
                if (elapsed < MovementSkillMath.RecastDelayMs)
                {
                    debugReason = $"Skipped: local recast delay active ({elapsed}ms elapsed, need {MovementSkillMath.RecastDelayMs}ms).";
                    return false;
                }
            }

            if (!MovementSkillMath.ShouldAttemptMovementSkill(
                movementSkillsEnabled,
                builtPath,
                remainingNodes,
                minimumNodes,
                now,
                lastSkillUseTimestampMs,
                MovementSkillMath.RecastDelayMs))
            {
                debugReason = "Skipped: movement skill gate returned false.";
                return false;
            }

            // Spec 13/20: the cast is gated until ~200ms after pathfinding to the current target started; walk-clicks keep flowing during the delay because this returns false and the caller falls through to the normal walk click.
            if (!string.Equals(targetPath, _castDelayTargetPath, StringComparison.Ordinal))
            {
                _castDelayTargetPath = targetPath;
                _castDelayStartTimestampMs = now;
            }

            long castDelayElapsed = now - _castDelayStartTimestampMs;
            if (castDelayElapsed < MovementSkillMath.CastDelayMs)
            {
                debugReason = $"Skipped: movement skill cast delay active ({castDelayElapsed}ms elapsed, need {MovementSkillMath.CastDelayMs}ms).";
                return false;
            }

            if (!TryResolveMovementSkillCastPosition(targetScreen, targetPath, out castPoint))
            {
                debugReason = "Skipped: unable to resolve safe/clickable movement-skill cast point.";
                return false;
            }

            object? skillBar = _dependencies.GameController?.IngameState?.IngameUi?.SkillBar;
            if (!MovementSkillBindingResolver.TryFindReadyMovementSkillBinding(skillBar, out MovementSkillBinding binding, out string skillSearchDebug))
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
                Input.SetCursorPos(new NumVector2(castPoint.X, castPoint.Y));
                ClickPipelineTiming.Sleep(10);
            }

            Keyboard.KeyPress(binding.BoundKey, MovementSkillMath.KeyTapDelayMs);
            _dependencies.RuntimeState.LastMovementSkillUseTimestampMs = now;
            int postCastClickBlockMs = ResolveMovementSkillPostCastClickBlockMsForCast(binding.InternalName);
            _dependencies.RuntimeState.MovementSkillPostCastClickBlockUntilTimestampMs = postCastClickBlockMs > 0 ? now + postCastClickBlockMs : 0;
            int statusPollWindowMs = MovementSkillMath.ResolveMovementSkillStatusPollWindowMs(postCastClickBlockMs, binding.InternalName);
            _dependencies.RuntimeState.MovementSkillStatusPollUntilTimestampMs = statusPollWindowMs > 0 ? now + statusPollWindowMs : 0;
            _dependencies.RuntimeState.LastUsedMovementSkillEntry = statusPollWindowMs > 0 ? binding.Entry : null;
            _dependencies.PerformanceMonitor.RecordClickInterval();
            _dependencies.DebugLog($"[TryUseMovementSkillForOffscreenPathing] Cast movement skill '{binding.InternalName}' with key '{binding.BoundKey}'");
            debugReason = $"Used movement skill '{binding.InternalName}' with key '{binding.BoundKey}' (remainingNodes={remainingNodes}, minNodes={minimumNodes}, postCastClickBlockMs={postCastClickBlockMs}, statusPollWindowMs={statusPollWindowMs}).";
            return true;
        }

        public bool TryGetMovementSkillPostCastBlockState(long now, out string reason)
        {
            reason = string.Empty;

            if (MovementSkillMath.IsMovementSkillPostCastClickBlocked(now, _dependencies.RuntimeState.MovementSkillPostCastClickBlockUntilTimestampMs, out long remainingMs))
            {
                reason = $"timing window active ({remainingMs}ms remaining)";
                return true;
            }

            long statusPollUntilTimestampMs = _dependencies.RuntimeState.MovementSkillStatusPollUntilTimestampMs;
            if (statusPollUntilTimestampMs <= 0 || now > statusPollUntilTimestampMs)
            {
                _dependencies.RuntimeState.MovementSkillStatusPollUntilTimestampMs = 0;
                _dependencies.RuntimeState.LastUsedMovementSkillEntry = null;
                return false;
            }

            if (!MovementSkillBindingResolver.TryResolveMovementSkillRuntimeState(_dependencies.RuntimeState.LastUsedMovementSkillEntry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed))
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

            _dependencies.RuntimeState.MovementSkillStatusPollUntilTimestampMs = 0;
            _dependencies.RuntimeState.LastUsedMovementSkillEntry = null;
            return false;
        }

        public int ResolveMovementSkillPostCastClickBlockMsForCast(string? movementSkillInternalName)
        {
            int resolved = MovementSkillMath.ResolveMovementSkillPostCastClickBlockMs(movementSkillInternalName);
            if (!MovementSkillMath.IsShieldChargeMovementSkill(movementSkillInternalName))
                return resolved;

            return SystemMath.Max(0, _dependencies.Settings.OffscreenShieldChargePostCastClickDelayMs?.Value ?? MovementSkillMath.ShieldChargePostCastClickBlockMs);
        }

        private bool TryResolveMovementSkillCastPosition(Vector2 targetScreen, string targetPath, out Vector2 castPoint)
        {
            castPoint = default;
            GameController controller = _dependencies.GameController;
            if (controller?.Window == null)
                return false;

            RectangleF win;
            try
            {
                win = controller.Window.GetWindowRectangleTimeCache;
            }
            catch
            {
                return false;
            }

            return MovementSkillCastPointResolver.TryResolveCastPoint(win, targetScreen, targetPath, _dependencies.PointIsInClickableArea, out castPoint);
        }
    }
}
