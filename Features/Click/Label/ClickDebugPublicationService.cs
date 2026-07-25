namespace ClickIt.Features.Click.Label
{
    internal readonly record struct ClickDebugPublicationServiceDependencies(
        GameController GameController,
        Func<bool> ShouldCaptureClickDebug,
        Action<ClickDebugSnapshot> SetLatestClickDebug,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace);

    internal sealed class ClickDebugPublicationService(ClickDebugPublicationServiceDependencies dependencies)
    {
        private readonly ClickDebugPublicationServiceDependencies _dependencies = dependencies;

        internal bool ShouldCaptureClickDebug()
            => _dependencies.ShouldCaptureClickDebug();

        internal void PublishClickDebugSnapshot(ClickDebugSnapshot snapshot)
        {
            if (!_dependencies.ShouldCaptureClickDebug())
                return;

            _dependencies.SetLatestClickDebug(snapshot);
        }

        internal void PublishSettlersClickDebugSnapshot(
            string stage,
            string mechanicId,
            string entityPath,
            float distance,
            Vector2 worldScreenRaw,
            Vector2 worldScreenAbsolute,
            Vector2 resolvedClickPoint,
            bool resolved,
            string notes)
        {
            PublishClickDebugSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId,
                EntityPath: entityPath,
                Distance: distance,
                WorldScreenRaw: worldScreenRaw,
                WorldScreenAbsolute: worldScreenAbsolute,
                ResolvedClickPoint: resolvedClickPoint,
                Resolved: resolved,
                CenterInWindow: _dependencies.IsInsideWindowInEitherSpace(worldScreenAbsolute),
                CenterClickable: _dependencies.IsClickableInEitherSpace(worldScreenAbsolute, entityPath),
                ResolvedInWindow: resolved && _dependencies.IsInsideWindowInEitherSpace(resolvedClickPoint),
                ResolvedClickable: resolved && _dependencies.IsClickableInEitherSpace(resolvedClickPoint, entityPath),
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        internal void PublishClickFlowDebugStage(string stage, string notes, string? mechanicId = null)
        {
            PublishClickDebugSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId ?? string.Empty,
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
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        internal void PublishLabelClickDebug(
            string stage,
            string? mechanicId,
            LabelOnGround label,
            Vector2 resolvedClickPos,
            bool resolved,
            string notes)
        {
            if (!ShouldCaptureClickDebug())
                return;

            Entity? entity = label?.ItemOnGround;
            if (entity == null)
                return;

            string entityPath = entity.Path ?? string.Empty;
            NumVector2 worldScreenRawVec = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 worldScreenAbsolute = worldScreenRaw + windowTopLeft;

            bool centerInWindow = _dependencies.IsInsideWindowInEitherSpace(worldScreenAbsolute);
            bool centerClickable = _dependencies.IsClickableInEitherSpace(worldScreenAbsolute, entityPath);
            bool resolvedInWindow = _dependencies.IsInsideWindowInEitherSpace(resolvedClickPos);
            bool resolvedClickable = _dependencies.IsClickableInEitherSpace(resolvedClickPos, entityPath);

            PublishClickDebugSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId ?? string.Empty,
                EntityPath: entityPath,
                Distance: entity.DistancePlayer,
                WorldScreenRaw: worldScreenRaw,
                WorldScreenAbsolute: worldScreenAbsolute,
                ResolvedClickPoint: resolvedClickPos,
                Resolved: resolved,
                CenterInWindow: centerInWindow,
                CenterClickable: centerClickable,
                ResolvedInWindow: resolvedInWindow,
                ResolvedClickable: resolvedClickable,
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }
    }
}