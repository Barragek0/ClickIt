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
                    labelScanMs = SystemMath.Max(0, Environment.TickCount64 - diagnosticsStartMs);
                    diagnosticsStartMs = Environment.TickCount64;
                }

                EntityQueryService.VisitValidEntities(_dependencies.GameController, entity =>
                {
                    if (collectDiagnostics)
                        scanned++;

                    bool isValid = DynamicAccess.TryReadBool(entity, static e => e.IsValid, out bool resolvedIsValid) && resolvedIsValid;
                    float distance = DynamicAccess.TryReadFloat(entity, static e => e.DistancePlayer, out float resolvedDistance)
                        ? resolvedDistance
                        : float.MaxValue;

                    if (VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(isValid, distance, _dependencies.Settings.ClickDistance.Value))
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
                        if (captureClickDebug)
                            PublishSettlersCandidateDebug("CandidateSelected", candidate, "Nearest settlers candidate selected");


                    return false;
                });

                long entityScanMs = collectDiagnostics
                    ? SystemMath.Max(0, Environment.TickCount64 - diagnosticsStartMs)
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
                && DynamicAccess.TryGetDynamicValue(entity, static e => e.Address, out object? rawAddress)
                && rawAddress is long entityAddress
                && OffscreenPathingMath.IsBackedByGroundLabel(entityAddress, labelEntityAddresses);

            if (!hasGroundLabel)
                return false;

            attemptedProbe = true;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            if (!VisibleMechanicClickablePointResolver.TryResolveEntityClickablePoint(
                _dependencies.GameController,
                entity,
                path,
                windowTopLeft,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.IsClickableInEitherSpace,
                out Vector2 clickPos,
                out Vector2 worldScreenRaw,
                out Vector2 worldScreenAbsolute))
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
            path = DynamicAccess.TryReadString(entity, static e => e.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;

            bool isValid = DynamicAccess.TryReadBool(entity, static e => e.IsValid, out bool resolvedIsValid) && resolvedIsValid;
            float distance = DynamicAccess.TryReadFloat(entity, static e => e.DistancePlayer, out float resolvedDistance)
                ? resolvedDistance
                : float.MaxValue;

            if (!MechanicClassifier.TryGetSettlersOreMechanicId(path, out string? resolvedMechanic)
                || string.IsNullOrWhiteSpace(resolvedMechanic))
                return false;


            if (!SettlersMechanicPolicy.IsEnabled(_dependencies.Settings, resolvedMechanic)
                || VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(isValid, distance, _dependencies.Settings.ClickDistance.Value))
                return false;


            mechanicId = resolvedMechanic;
            return true;
        }

        private void PublishSettlersProbeFailedDebug(Entity entity, string mechanicId, string path, Vector2 worldScreenRaw, Vector2 worldScreenAbsolute)
        {
            _dependencies.ClickDebugPublisher.PublishSettlersClickDebugSnapshot(
                stage: "ProbeFailed",
                mechanicId: mechanicId,
                entityPath: path,
                distance: DynamicAccess.TryReadFloat(entity, static e => e.DistancePlayer, out float distance)
                    ? distance
                    : float.MaxValue,
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