namespace ClickIt.Features.Blight;

// Single source of truth for the Blight tower palette so every consumer renders the same hue; stores opaque RGB.
internal static class BlightTowerColors
{
    private static readonly Vector4[] s_rgb = BuildRgb();

    private static Vector4[] BuildRgb()
    {
        var colors = new Vector4[Enum.GetValues<BlightTowerType>().Length];
        colors[(int)BlightTowerType.Chilling] = new(0.30f, 0.62f, 1.00f, 1f);   // blue
        colors[(int)BlightTowerType.ShockNova] = new(0.95f, 0.90f, 0.20f, 1f);  // yellow — Arc/ShockNova family
        colors[(int)BlightTowerType.Empowering] = new(0.35f, 0.85f, 0.35f, 1f); // green
        colors[(int)BlightTowerType.Seismic] = new(1.00f, 0.50f, 0.10f, 1f);    // orange
        colors[(int)BlightTowerType.Summoning] = new(0.72f, 0.42f, 1.00f, 1f);  // purple — Scout/Summoning family
        colors[(int)BlightTowerType.Fireball] = new(0.95f, 0.35f, 0.30f, 1f);   // red — Meteor/Fireball family
        return colors;
    }

    internal static Vector4 AsVector4(BlightTowerType type) => s_rgb[(int)type];

    internal static Color AsColor(BlightTowerType type)
    {
        Vector4 c = s_rgb[(int)type];
        return new Color(
            (byte)MathF.Round(c.X * 255f, MidpointRounding.AwayFromZero),
            (byte)MathF.Round(c.Y * 255f, MidpointRounding.AwayFromZero),
            (byte)MathF.Round(c.Z * 255f, MidpointRounding.AwayFromZero));
    }
}
