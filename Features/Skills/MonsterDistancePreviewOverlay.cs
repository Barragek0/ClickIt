namespace ClickIt.Features.Skills;

internal sealed class MonsterDistancePreviewOverlay : IOverlay
{
    private sealed record PlayerPosition(System.Numerics.Vector3 Center, long TimestampMs);
    private PlayerPosition? _playerPosition;

    public string Name => "MonsterDistancePreview";
    public RenderSection Section => RenderSection.LazyMode;
    public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(50);
    public TimingChannel? RefreshTimingChannel => null;
    public ProcessingSection ProcessingSection => ProcessingSection.Unknown;
    public bool IsEnabled(ClickItSettings settings) => settings.TransientState.DistancePreview != null;

    public void Refresh(OverlayRefreshContext ctx)
    {
        if (ctx.Settings.TransientState.DistancePreview == null)
        {
            Volatile.Write(ref _playerPosition, null);
            return;
        }
        try
        {
            Entity? player = ctx.GameController?.Player;
            if (player == null || !player.IsValid)
            {
                Volatile.Write(ref _playerPosition, null);
                return;
            }
            NumVector2 grid = player.GridPosNum;
            float z = player.PosNum.Z;
            const float scale = 1f / PoeMapExtension.WorldToGridConversion;
            Volatile.Write(ref _playerPosition, new PlayerPosition(new System.Numerics.Vector3(grid.X * scale, grid.Y * scale, z), Environment.TickCount64));
        }
        catch
        {
            Volatile.Write(ref _playerPosition, null);
        }
    }

    public void Draw(OverlayRenderContext ctx)
    {
        PlayerPosition? player = Volatile.Read(ref _playerPosition);
        if (player == null || Environment.TickCount64 - player.TimestampMs > 500)
            return;
        int distance = ResolveDistance(ctx.Settings, ctx.Settings.TransientState.DistancePreview);
        if (distance <= 0)
            return;
        ctx.DrawQueue.EnqueueCircleInWorld(player.Center, distance / PoeMapExtension.WorldToGridConversion, new Color(50, 255, 100, 220), 2, 96, false);
    }

    internal static int ResolveDistance(ClickItSettings settings, string? id)
        => id switch
        {
            "VaalNormal" => SystemMath.Clamp(settings.VaalMoltenShellNormalDistance.Value, 1, 99),
            "VaalMagic" => SystemMath.Clamp(settings.VaalMoltenShellMagicDistance.Value, 1, 99),
            "VaalRare" => SystemMath.Clamp(settings.VaalMoltenShellRareDistance.Value, 1, 99),
            "VaalUnique" => SystemMath.Clamp(settings.VaalMoltenShellUniqueDistance.Value, 1, 99),
            "LazyNormal" => SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeNormalMonsterBlockDistance),
            "LazyMagic" => SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeMagicMonsterBlockDistance),
            "LazyRare" => SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeRareMonsterBlockDistance),
            "LazyUnique" => SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(settings.LazyModeUniqueMonsterBlockDistance),
            "ClickSearchRadius" => SystemMath.Clamp(settings.ClickDistance.Value, 0, 300),
            _ => 0
        };
}
