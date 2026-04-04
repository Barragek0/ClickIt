namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct SettlersOreTargetSelectorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ClickDebugPublicationService ClickDebugPublisher,
        Action<string> DebugLog,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        GroundLabelEntityAddressProvider GroundLabelEntityAddresses);

    internal sealed class SettlersOreTargetSelector(SettlersOreTargetSelectorDependencies dependencies)
    {
        private readonly SettlersOreTargetSelectorDependencies _dependencies = dependencies;

        internal SettlersOreCandidate? ResolveNextSettlersOreCandidate()
        {
            if (!_dependencies.Settings.ClickSettlersOre.Value)
                return null;

            bool captureClickDebug = _dependencies.ClickDebugPublisher.ShouldCaptureClickDebug();
            bool collectDiagnostics = captureClickDebug || _dependencies.Settings.DebugMode?.Value == true;

            try
            {
                SettlersOreCandidate? best = null;
                RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = ManualCursorSelectionMath.GetCursorAbsolutePosition();
                int scanned = 0;
                int prefiltered = 0;
                int mechanicMatched = 0;
                int probeAttempts = 0;
                int probeResolved = 0;
                int labelBacked = 0;
                long labelScanMs = 0;
                long diagnosticsStartMs = collectDiagnostics ? Environment.TickCount64 : 0;
                IReadOnlySet<long>? labelEntityAddresses = _dependencies.GroundLabelEntityAddresses.Collect();
                if (collectDiagnostics)
                {
                    labelScanMs = Math.Max(0, Environment.TickCount64 - diagnosticsStartMs);
                    diagnosticsStartMs = Environment.TickCount64;
                }

                EntityQueryService.VisitValidEntities(_dependencies.GameController, entity =>
                {
                    if (collectDiagnostics)
                        scanned++;

                    if (VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, _dependencies.Settings.ClickDistance.Value))
                    {
                        if (collectDiagnostics)
                            prefiltered++;
                        return false;
                    }

                    if (!TryBuildSettlersCandidate(
                            entity,
                            windowArea,
                            labelEntityAddresses,
                            captureClickDebug,
                            out SettlersOreCandidate candidate,
                            out bool hadLabel,
                            out bool matchedMechanic,
                            out bool attemptedProbe))
                    {
                        if (collectDiagnostics && matchedMechanic)
                            mechanicMatched++;
                        if (collectDiagnostics && attemptedProbe)
                            probeAttempts++;

                        return false;
                    }

                    if (collectDiagnostics && matchedMechanic)
                        mechanicMatched++;
                    if (collectDiagnostics && attemptedProbe)
                        probeAttempts++;
                    if (collectDiagnostics)
                        probeResolved++;

                    if (collectDiagnostics && hadLabel)
                        labelBacked++;

                    if (MechanicCandidateResolver.TryPromoteSettlersCandidate(ref best, candidate, cursorAbsolute, windowTopLeft))
                    {
                        if (captureClickDebug)
                            PublishSettlersCandidateDebug("CandidateSelected", candidate, "Nearest settlers candidate selected");
                    }

                    return false;
                });

                long entityScanMs = collectDiagnostics
                    ? Math.Max(0, Environment.TickCount64 - diagnosticsStartMs)
                    : 0;

                if (!best.HasValue && collectDiagnostics)
                {
                    _dependencies.DebugLog($"[ResolveNextSettlersOreCandidate] none scanned:{scanned} prefiltered:{prefiltered} mechanicMatched:{mechanicMatched} probeAttempts:{probeAttempts} probeResolved:{probeResolved} labelBacked:{labelBacked} labelScanMs:{labelScanMs} entityScanMs:{entityScanMs}");
                    if (captureClickDebug)
                        PublishNoSettlersCandidateDebug(scanned, prefiltered, mechanicMatched, probeAttempts, probeResolved, labelBacked, labelScanMs, entityScanMs);
                }

                return best;
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog($"[ResolveNextSettlersOreCandidate] Failed to scan entities: {ex.Message}");
                return null;
            }
        }

        private void PublishNoSettlersCandidateDebug(
            int scanned,
            int prefiltered,
            int mechanicMatched,
            int probeAttempts,
            int probeResolved,
            int labelBacked,
            long labelScanMs,
            long entityScanMs)
        {
            _dependencies.ClickDebugPublisher.PublishClickDebugSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: "NoCandidate",
                MechanicId: string.Empty,
                EntityPath: string.Empty,
                Distance: 0f,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: false,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: $"scanned={scanned}, prefiltered={prefiltered}, mechanicMatched={mechanicMatched}, probeAttempts={probeAttempts}, probeResolved={probeResolved}, labelBacked={labelBacked}, labelScanMs={labelScanMs}, entityScanMs={entityScanMs}",
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private bool TryBuildSettlersCandidate(
            Entity entity,
            RectangleF windowArea,
            IReadOnlySet<long>? labelEntityAddresses,
            bool captureClickDebug,
            out SettlersOreCandidate candidate,
            out bool hasGroundLabel,
            out bool matchedMechanic,
            out bool attemptedProbe)
        {
            candidate = default;
            hasGroundLabel = false;
            matchedMechanic = false;
            attemptedProbe = false;

            if (!TryResolveSettlersMechanic(entity, out string mechanicId, out string path))
                return false;

            matchedMechanic = true;

            hasGroundLabel = labelEntityAddresses != null
                && OffscreenPathingMath.IsBackedByGroundLabel(entity.Address, labelEntityAddresses);

            if (!hasGroundLabel)
                return false;

            var worldScreenRawVec = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);

            attemptedProbe = true;
            if (!ClickableProbeResolver.TryResolveNearbyClickablePoint(
                    worldScreenAbsolute,
                    path,
                    _dependencies.IsInsideWindowInEitherSpace,
                    _dependencies.IsClickableInEitherSpace,
                    out Vector2 clickPos))
            {
                if (captureClickDebug)
                    PublishSettlersProbeFailedDebug(entity, mechanicId, path, worldScreenRaw, worldScreenAbsolute);
                return false;
            }

            candidate = new SettlersOreCandidate(entity, clickPos, mechanicId, path, worldScreenRaw, worldScreenAbsolute);
            if (captureClickDebug)
                PublishSettlersProbeResolvedDebug(candidate);

            return true;
        }

        private bool TryResolveSettlersMechanic(Entity entity, out string mechanicId, out string path)
        {
            mechanicId = string.Empty;
            path = entity.Path ?? string.Empty;

            if (!MechanicClassifier.TryGetSettlersOreMechanicId(path, out string? resolvedMechanic)
                || string.IsNullOrWhiteSpace(resolvedMechanic))
            {
                return false;
            }

            if (!SettlersMechanicPolicy.IsEnabled(_dependencies.Settings, resolvedMechanic)
                || VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, _dependencies.Settings.ClickDistance.Value))
            {
                return false;
            }

            mechanicId = resolvedMechanic;
            return true;
        }

        private void PublishSettlersProbeFailedDebug(Entity entity, string mechanicId, string path, Vector2 worldScreenRaw, Vector2 worldScreenAbsolute)
        {
            _dependencies.ClickDebugPublisher.PublishSettlersClickDebugSnapshot(
                stage: "ProbeFailed",
                mechanicId: mechanicId,
                entityPath: path,
                distance: entity.DistancePlayer,
                worldScreenRaw: worldScreenRaw,
                worldScreenAbsolute: worldScreenAbsolute,
                resolvedClickPoint: default,
                resolved: false,
                notes: "No nearby clickable point resolved");
        }

        private void PublishSettlersProbeResolvedDebug(SettlersOreCandidate candidate)
        {
            _dependencies.ClickDebugPublisher.PublishSettlersClickDebugSnapshot(
                stage: "ProbeResolved",
                mechanicId: candidate.MechanicId,
                entityPath: candidate.EntityPath,
                distance: candidate.Distance,
                worldScreenRaw: candidate.WorldScreenRaw,
                worldScreenAbsolute: candidate.WorldScreenAbsolute,
                resolvedClickPoint: candidate.ClickPosition,
                resolved: true,
                notes: "Resolved nearby clickable point");
        }

        private void PublishSettlersCandidateDebug(string stage, SettlersOreCandidate candidate, string notes)
        {
            _dependencies.ClickDebugPublisher.PublishSettlersClickDebugSnapshot(
                stage: stage,
                mechanicId: candidate.MechanicId,
                entityPath: candidate.EntityPath,
                distance: candidate.Distance,
                worldScreenRaw: candidate.WorldScreenRaw,
                worldScreenAbsolute: candidate.WorldScreenAbsolute,
                resolvedClickPoint: candidate.ClickPosition,
                resolved: true,
                notes: notes);
        }
    }
}