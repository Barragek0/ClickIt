namespace ClickIt.Features.Blight.Debug;

internal static class BlightChestDebug
{
    private const string BlightChestPathMarker = "Chests/Blight";

    internal static bool IsBlightChestPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
           && path.Contains(BlightChestPathMarker, StringComparison.OrdinalIgnoreCase);

    internal static bool IsBlightChestMechanic(string? mechanicId)
        => string.Equals(mechanicId, MechanicIds.BlightCyst, StringComparison.OrdinalIgnoreCase);
}
